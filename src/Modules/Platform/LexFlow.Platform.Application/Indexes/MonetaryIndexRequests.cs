using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.Indexes;
using LexFlow.Platform.Domain.Indexes;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Indexes;

/// <summary>Variação de um índice num mês, em %, e de onde veio.</summary>
public sealed record MonetaryIndexResponse(MonetaryIndexCode Index, DateOnly Month, decimal Percent, string Source);

/// <summary>Os valores de um índice entre dois meses (inclusive), do mais antigo ao mais recente.</summary>
public sealed record ListMonetaryIndexesQuery(MonetaryIndexCode Index, DateOnly? From, DateOnly? To) : IQuery<IReadOnlyList<MonetaryIndexResponse>>;

internal sealed class ListMonetaryIndexesValidator : AbstractValidator<ListMonetaryIndexesQuery>
{
    public ListMonetaryIndexesValidator()
    {
        RuleFor(q => q.Index).IsInEnum().WithErrorCode(IndexCodes.InvalidIndex.Code);
        RuleFor(q => q).Must(q => q.From is null || q.To is null || q.From <= q.To)
            .WithErrorCode(MonetaryCorrectionCalculator.PeriodInvalid.Code)
            .OverridePropertyName("from");
    }
}

internal sealed class ListMonetaryIndexesHandler(IPlatformDbContext db) : IQueryHandler<ListMonetaryIndexesQuery, IReadOnlyList<MonetaryIndexResponse>>
{
    public async Task<Result<IReadOnlyList<MonetaryIndexResponse>>> Handle(ListMonetaryIndexesQuery query, CancellationToken cancellationToken)
    {
        var kind = IndexCodes.ToKind(query.Index);
        var rows = db.MonetaryIndexes.AsNoTracking().Where(i => i.Kind == kind);
        if (query.From is { } from)
        {
            var first = MonetaryIndex.FirstDayOf(from);
            rows = rows.Where(i => i.Month >= first);
        }

        if (query.To is { } to)
        {
            var last = MonetaryIndex.FirstDayOf(to);
            rows = rows.Where(i => i.Month <= last);
        }

        var list = await rows.OrderBy(i => i.Month).Take(MonetaryCorrectionCalculator.MaxMonths)
            .Select(i => new { i.Month, i.Percent, i.Source })
            .ToListAsync(cancellationToken);
        return list.Select(i => new MonetaryIndexResponse(query.Index, i.Month, i.Percent, i.Source)).ToList();
    }
}

/// <summary>A calculadora de correção monetária (<c>POST /api/v1/monetary-corrections</c>).</summary>
public sealed record CalculateCorrectionQuery(MonetaryCorrectionRequest Request) : IQuery<MonetaryCorrectionResult>;

internal sealed class CalculateCorrectionValidator : AbstractValidator<CalculateCorrectionQuery>
{
    public CalculateCorrectionValidator()
    {
        RuleFor(q => q.Request).NotNull();
        RuleFor(q => q.Request.Index).IsInEnum().WithErrorCode(IndexCodes.InvalidIndex.Code);
    }
}

internal sealed class CalculateCorrectionHandler(IMonetaryCorrection correction) : IQueryHandler<CalculateCorrectionQuery, MonetaryCorrectionResult>
{
    public Task<Result<MonetaryCorrectionResult>> Handle(CalculateCorrectionQuery query, CancellationToken cancellationToken) =>
        correction.CalculateAsync(query.Request, cancellationToken);
}

/// <summary>
/// Correção com a tabela da plataforma: carrega os meses do período (uma leitura por índice, mesmo em lote) e delega
/// à regra do domínio.
/// </summary>
internal sealed class MonetaryCorrection(IPlatformDbContext db) : IMonetaryCorrection
{
    public async Task<Result<MonetaryCorrectionResult>> CalculateAsync(MonetaryCorrectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (await CalculateManyAsync([request], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<Result<MonetaryCorrectionResult>>> CalculateManyAsync(IReadOnlyList<MonetaryCorrectionRequest> requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        var tables = new Dictionary<MonetaryIndexCode, Dictionary<DateOnly, decimal>>();
        foreach (var group in requests.Where(r => Enum.IsDefined(r.Index) && r.CorrectFrom < r.Until).GroupBy(r => r.Index))
        {
            var kind = IndexCodes.ToKind(group.Key);
            var first = MonetaryIndex.FirstDayOf(group.Min(r => r.CorrectFrom));
            var last = MonetaryIndex.FirstDayOf(group.Max(r => r.Until));
            tables[group.Key] = await db.MonetaryIndexes.AsNoTracking()
                .Where(i => i.Kind == kind && i.Month >= first && i.Month < last)
                .ToDictionaryAsync(i => i.Month, i => i.Percent, cancellationToken);
        }

        return [.. requests.Select(r => Calculate(r, tables.GetValueOrDefault(r.Index) ?? []))];
    }

    private static Result<MonetaryCorrectionResult> Calculate(MonetaryCorrectionRequest request, Dictionary<DateOnly, decimal> percents)
    {
        if (!Enum.IsDefined(request.Index))
        {
            return IndexCodes.InvalidIndex;
        }

        var input = new CorrectionInput(request.Principal, IndexCodes.ToKind(request.Index), request.CorrectFrom, request.Until, request.InterestFrom, request.MonthlyInterestPercent);
        var outcome = MonetaryCorrectionCalculator.Calculate(input, percents);
        if (outcome.IsFailure)
        {
            return outcome.Error;
        }

        var o = outcome.Value;
        return new MonetaryCorrectionResult(
            o.Principal,
            request.Index,
            o.Factor,
            o.CorrectedAmount,
            o.DeflationFloorApplied,
            o.InterestMonths,
            o.InterestPercent,
            o.InterestAmount,
            o.Total,
            [.. o.Months.Select(m => new CorrectionMonthLine(m.Month, m.Percent, m.AccumulatedFactor))]);
    }
}

/// <summary>Conversão entre o índice do contrato e o do domínio (os nomes são os mesmos).</summary>
internal static class IndexCodes
{
    public static readonly Error InvalidIndex = Error.Validation("platform.monetary_index_invalid", "Índice inválido.");

    public static MonetaryIndexKind ToKind(MonetaryIndexCode code) => code switch
    {
        MonetaryIndexCode.IpcaE => MonetaryIndexKind.IpcaE,
        MonetaryIndexCode.Inpc => MonetaryIndexKind.Inpc,
        MonetaryIndexCode.Selic => MonetaryIndexKind.Selic,
        MonetaryIndexCode.Tr => MonetaryIndexKind.Tr,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };
}

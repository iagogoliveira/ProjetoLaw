using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Platform.Contracts.Calendar;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.Calendar;

/// <summary>
/// Pede a sugestão de prazo. O local vem do processo (tribunal e comarca), mas pode ser informado ou corrigido na própria
/// consulta; a resposta diz qual local foi usado. Nada é gravado.
/// </summary>
public sealed record CalculateDeadlineQuery(Guid? CaseId, Guid? CourtId, string? StateCode, string? Municipality,
    DeadlineStartKind StartKind, DateOnly TriggerDate, DateOnly? ConsultedOn, int Days, CountingMode Mode, bool Doubled,
    int InternalLeadDays) : IQuery<DeadlineCalculationResponse>;

public sealed record CalculationLineResponse(DateOnly Date, CalculationStep Step, int? Count, string? Reason);

public sealed record DeadlineCalculationResponse(DateOnly IntimationOn, DateOnly StartsOn, DateOnly FatalDueOn, DateOnly InternalDueOn,
    int CountedDays, Guid? CourtId, string? StateCode, string? Municipality, bool FederalJustice,
    IReadOnlyList<CalculationLineResponse> Lines);

internal sealed class CalculateDeadlineValidator : AbstractValidator<CalculateDeadlineQuery>
{
    public CalculateDeadlineValidator()
    {
        RuleFor(q => q.Days).InclusiveBetween(1, DeadlineCalculator.MaxDays);
        RuleFor(q => q.InternalLeadDays).InclusiveBetween(0, DeadlineCalculator.MaxInternalLeadDays);
        RuleFor(q => q.StartKind).IsInEnum();
        RuleFor(q => q.Mode).IsInEnum();
        RuleFor(q => q.StateCode).Length(2).When(q => q.StateCode is not null);
        RuleFor(q => q.Municipality).MaximumLength(120);
    }
}

internal sealed class CalculateDeadlineAuthorizer(ICaseDirectory cases) : IResourceAuthorizer<CalculateDeadlineQuery>
{
    public Task<Result> AuthorizeAsync(CalculateDeadlineQuery request, CancellationToken cancellationToken) =>
        request.CaseId is { } caseId ? cases.AuthorizeAsync(caseId, "schedule.deadlines.manage", cancellationToken) : Task.FromResult(Result.Success);
}

internal sealed class CalculateDeadlineHandler(ISchedulingDbContext db, ICaseDirectory cases, ICourtDirectory courts,
    IHolidayDirectory holidays, IDeadlineCalculator calculator) : IQueryHandler<CalculateDeadlineQuery, DeadlineCalculationResponse>
{
    /// <summary>Janela carregada a partir do fato: cobre 365 dias úteis em dobro com folga para recessos e feriados.</summary>
    private const int CalendarYears = 4;

    /// <summary>Segmentos do CNJ cujo tribunal atende uma só UF: 6 (Eleitoral), 8 (Estadual) e 9 (Militar estadual).</summary>
    private static readonly int[] SingleStateSegments = [6, 8, 9];

    /// <summary>TRTs que abrangem mais de uma UF (8ª, 10ª, 11ª e 14ª Regiões): a UF da vara precisa ser informada.</summary>
    private static readonly string[] MultiStateLaborRegions = ["08", "10", "11", "14"];

    public async Task<Result<DeadlineCalculationResponse>> Handle(CalculateDeadlineQuery query, CancellationToken cancellationToken)
    {
        var location = query.CaseId is { } caseId ? await cases.LocationAsync(caseId, cancellationToken) : null;
        var courtId = query.CourtId ?? location?.CourtId;
        var court = courtId is { } id ? await courts.GetAsync(id, cancellationToken) : null;
        if (courtId is not null && court is null) { return SchedulingErrors.CourtNotFound; }

        var state = Normalize(query.StateCode) ?? DefaultState(court);
        var municipality = string.IsNullOrWhiteSpace(query.Municipality) ? location?.District?.Trim() : query.Municipality.Trim();
        var federal = court?.Segment == 4;

        var from = query.TriggerDate;
        var to = from.AddYears(CalendarYears);
        var platform = await holidays.ForAsync(from, to, state, federal, cancellationToken);
        var office = (await db.CalendarEntries.AsNoTracking().ToListAsync(cancellationToken))
            .Where(e => e.AppliesTo(state, municipality, court?.Id))
            .SelectMany(e => e.DaysBetween(from, to));
        var calendar = new WorkingCalendar(from, to,
            platform.Select(d => new NonWorkingDay(d.Date, $"{d.Name} ({d.LegalBasis})", d.IsSuspension)).Concat(office));

        var result = calculator.Calculate(new DeadlineCalculationInput(query.StartKind, query.TriggerDate, query.ConsultedOn, query.Days,
            query.Mode, query.Doubled, query.InternalLeadDays), calendar);
        if (result.IsFailure) { return result.Error; }

        var c = result.Value;
        return new DeadlineCalculationResponse(c.IntimationOn, c.StartsOn, c.FatalDueOn, c.InternalDueOn, c.CountedDays, court?.Id, state,
            municipality, federal, [.. c.Lines.Select(l => new CalculationLineResponse(l.Date, l.Step, l.Count, l.Reason))]);
    }

    private static string? Normalize(string? state) => string.IsNullOrWhiteSpace(state) ? null : state.Trim().ToUpperInvariant();

    /// <summary>A UF do tribunal só é presumida quando ele atende uma única UF; nos demais casos ela vem da consulta.</summary>
    internal static string? DefaultState(CourtInfo? court) => court switch
    {
        null => null,
        { Segment: 5 } when !MultiStateLaborRegions.Contains(court.Code, StringComparer.Ordinal) => court.State,
        _ when SingleStateSegments.Contains(court.Segment) => court.State,
        _ => null,
    };
}

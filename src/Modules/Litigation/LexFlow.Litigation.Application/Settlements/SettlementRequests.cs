using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Cases;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Settlements;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Settlements;

public sealed record SettlementOfferResponse(int Round, OfferParty Party, decimal Amount, string? Terms, DateOnly OfferedOn);

public sealed record SettlementInstallmentResponse(Guid Id, int Number, DateOnly DueOn, decimal Amount, DateOnly? PaidOn, decimal? PaidAmount, bool IsOverdue);

/// <summary>
/// Acordo com as rodadas, as parcelas e o indicador (RF-16): o valor pedido (soma dos pedidos do processo ou, sem
/// eles, o valor da causa) comparado com o valor acordado (ou, antes dele, a última proposta). Quando o cliente paga,
/// a diferença é a economia; quando recebe, é o deságio.
/// </summary>
public sealed record SettlementResponse(
    Guid Id,
    Guid CaseId,
    SettlementDirection Direction,
    SettlementStatus Status,
    string? Notes,
    decimal? AgreedAmount,
    IReadOnlyList<SettlementOfferResponse> Offers,
    IReadOnlyList<SettlementInstallmentResponse> Installments,
    decimal? RequestedAmount,
    decimal? Difference,
    decimal? DifferencePercent,
    Guid? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateOnly? HomologatedOn,
    string? Reason,
    DateTimeOffset CreatedAt);

public sealed record ListSettlementsQuery(Guid CaseId) : IQuery<IReadOnlyList<SettlementResponse>>;

public sealed record StartSettlementCommand(Guid CaseId, SettlementDirection Direction, string? Notes, OfferInput? FirstOffer) : ICommand<SettlementResponse>;

public sealed record AddSettlementOfferCommand(Guid CaseId, Guid SettlementId, OfferInput Offer) : ICommand<SettlementResponse>;

public sealed record SubmitSettlementCommand(Guid CaseId, Guid SettlementId, decimal AgreedAmount, IReadOnlyList<InstallmentInput>? Installments)
    : ICommand<SettlementResponse>;

public sealed record ApproveSettlementCommand(Guid CaseId, Guid SettlementId) : ICommand<SettlementResponse>;

public sealed record ReturnSettlementCommand(Guid CaseId, Guid SettlementId, string? Reason) : ICommand<SettlementResponse>;

public sealed record HomologateSettlementCommand(Guid CaseId, Guid SettlementId, DateOnly HomologatedOn) : ICommand<SettlementResponse>;

/// <summary>Encerra a negociação sem acordo (<see cref="Cancel"/> falso) ou cancela o acordo.</summary>
public sealed record EndSettlementCommand(Guid CaseId, Guid SettlementId, bool Cancel, string? Reason) : ICommand<SettlementResponse>;

public sealed record PayInstallmentCommand(Guid CaseId, Guid SettlementId, Guid InstallmentId, DateOnly PaidOn, decimal PaidAmount) : ICommand<SettlementResponse>;

internal sealed class StartSettlementValidator : AbstractValidator<StartSettlementCommand>
{
    public StartSettlementValidator()
    {
        RuleFor(c => c.Direction).IsInEnum().WithErrorCode("litigation.settlement_direction_invalid");
        RuleFor(c => c.Notes).MaximumLength(Settlement.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(c => c.FirstOffer!).SetValidator(new OfferValidator()).When(c => c.FirstOffer is not null);
    }
}

internal sealed class AddSettlementOfferValidator : AbstractValidator<AddSettlementOfferCommand>
{
    public AddSettlementOfferValidator()
    {
        RuleFor(c => c.Offer).NotNull().SetValidator(new OfferValidator());
    }
}

internal sealed class OfferValidator : AbstractValidator<OfferInput>
{
    public OfferValidator()
    {
        RuleFor(o => o.Party).IsInEnum().WithErrorCode("litigation.offer_party_invalid");
        RuleFor(o => o.Amount).GreaterThanOrEqualTo(0).WithErrorCode(LitigationErrors.ValueInvalid.Code);
        RuleFor(o => o.Terms).MaximumLength(Settlement.MaxTextLength).WithErrorCode("litigation.text_too_long");
    }
}

internal sealed class EndSettlementValidator : AbstractValidator<EndSettlementCommand>
{
    public EndSettlementValidator()
    {
        RuleFor(c => c.Reason).MaximumLength(Settlement.MaxTextLength).WithErrorCode("litigation.text_too_long");
    }
}

internal sealed class ReturnSettlementValidator : AbstractValidator<ReturnSettlementCommand>
{
    public ReturnSettlementValidator()
    {
        RuleFor(c => c.Reason).MaximumLength(Settlement.MaxTextLength).WithErrorCode("litigation.text_too_long");
    }
}

internal sealed class ListSettlementsHandler(ILitigationDbContext db, CaseWrites cases, SettlementResponses responses)
    : IQueryHandler<ListSettlementsQuery, IReadOnlyList<SettlementResponse>>
{
    public async Task<Result<IReadOnlyList<SettlementResponse>>> Handle(ListSettlementsQuery query, CancellationToken cancellationToken)
    {
        if (!await cases.ExistsAsync(query.CaseId, cancellationToken))
        {
            return LitigationErrors.CaseNotFound;
        }

        var settlements = await db.Settlements.AsNoTracking().Include(s => s.Offers).Include(s => s.Installments)
            .Where(s => s.LegalCaseId == query.CaseId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result.Ok(await responses.BuildAsync(query.CaseId, settlements, cancellationToken));
    }
}

internal sealed class StartSettlementHandler(ILitigationDbContext db, CaseWrites cases, IIdGenerator ids, SettlementResponses responses)
    : ICommandHandler<StartSettlementCommand, SettlementResponse>
{
    public async Task<Result<SettlementResponse>> Handle(StartSettlementCommand command, CancellationToken cancellationToken)
    {
        var open = await cases.CheckOpenAsync(command.CaseId, cancellationToken);
        if (open.IsFailure)
        {
            return open.Error!;
        }

        var settlement = Settlement.Start(ids.NewId(), command.CaseId, command.Direction, command.Notes, command.FirstOffer);
        if (settlement.IsFailure)
        {
            return settlement.Error!;
        }

        db.Settlements.Add(settlement.Value);
        await db.SaveChangesAsync(cancellationToken);
        return (await responses.BuildAsync(command.CaseId, [settlement.Value], cancellationToken))[0];
    }
}

internal sealed class AddSettlementOfferHandler(SettlementActions actions) : ICommandHandler<AddSettlementOfferCommand, SettlementResponse>
{
    public Task<Result<SettlementResponse>> Handle(AddSettlementOfferCommand command, CancellationToken cancellationToken) =>
        actions.RunAsync(command.CaseId, command.SettlementId, requireOpenCase: true, s => s.AddOffer(command.Offer), cancellationToken);
}

internal sealed class SubmitSettlementHandler(SettlementActions actions, CaseWrites cases) : ICommandHandler<SubmitSettlementCommand, SettlementResponse>
{
    public Task<Result<SettlementResponse>> Handle(SubmitSettlementCommand command, CancellationToken cancellationToken) =>
        actions.RunAsync(command.CaseId, command.SettlementId, requireOpenCase: true, s => s.Submit(command.AgreedAmount, command.Installments ?? [], cases.UserId, cases.Now), cancellationToken);
}

internal sealed class ApproveSettlementHandler(SettlementActions actions, CaseWrites cases) : ICommandHandler<ApproveSettlementCommand, SettlementResponse>
{
    public Task<Result<SettlementResponse>> Handle(ApproveSettlementCommand command, CancellationToken cancellationToken) =>
        actions.RunAsync(command.CaseId, command.SettlementId, requireOpenCase: false, s => s.Approve(cases.UserId, cases.Now), cancellationToken);
}

internal sealed class ReturnSettlementHandler(SettlementActions actions) : ICommandHandler<ReturnSettlementCommand, SettlementResponse>
{
    public Task<Result<SettlementResponse>> Handle(ReturnSettlementCommand command, CancellationToken cancellationToken) =>
        actions.RunAsync(command.CaseId, command.SettlementId, requireOpenCase: false, s => s.ReturnToNegotiation(command.Reason), cancellationToken);
}

internal sealed class EndSettlementHandler(SettlementActions actions) : ICommandHandler<EndSettlementCommand, SettlementResponse>
{
    public Task<Result<SettlementResponse>> Handle(EndSettlementCommand command, CancellationToken cancellationToken) =>
        actions.RunAsync(command.CaseId, command.SettlementId, requireOpenCase: false, s => command.Cancel ? s.Cancel(command.Reason) : s.Reject(command.Reason), cancellationToken);
}

internal sealed class PayInstallmentHandler(SettlementActions actions) : ICommandHandler<PayInstallmentCommand, SettlementResponse>
{
    public Task<Result<SettlementResponse>> Handle(PayInstallmentCommand command, CancellationToken cancellationToken) =>
        actions.RunAsync(command.CaseId, command.SettlementId, requireOpenCase: false, s => s.PayInstallment(command.InstallmentId, command.PaidOn, command.PaidAmount), cancellationToken);
}

/// <summary>A homologação publica <see cref="SettlementHomologatedIntegrationEvent"/> na mesma transação (outbox).</summary>
internal sealed class HomologateSettlementHandler(SettlementActions actions, IIntegrationEventPublisher events) : ICommandHandler<HomologateSettlementCommand, SettlementResponse>
{
    public Task<Result<SettlementResponse>> Handle(HomologateSettlementCommand command, CancellationToken cancellationToken) =>
        actions.RunAsync(command.CaseId, command.SettlementId, requireOpenCase: false, s =>
        {
            var homologated = s.Homologate(command.HomologatedOn);
            if (homologated.IsSuccess)
            {
                events.Publish(new SettlementHomologatedIntegrationEvent(
                    s.Id,
                    s.LegalCaseId,
                    s.Direction.ToString(),
                    Money.Brl,
                    s.AgreedAmount!.Value,
                    [.. s.Installments.OrderBy(i => i.Number).Select(i => new SettlementInstallmentLine(i.Id, i.Number, i.DueOn, i.Amount))],
                    command.HomologatedOn));
            }

            return homologated;
        },
        cancellationToken);
}

/// <summary>Carrega o acordo do processo, aplica a ação do domínio e grava.</summary>
internal sealed class SettlementActions(ILitigationDbContext db, CaseWrites cases, SettlementResponses responses)
{
    public async Task<Result<SettlementResponse>> RunAsync(
        Guid caseId, Guid settlementId, bool requireOpenCase, Func<Settlement, Result> action, CancellationToken cancellationToken)
    {
        if (requireOpenCase)
        {
            var open = await cases.CheckOpenAsync(caseId, cancellationToken);
            if (open.IsFailure)
            {
                return open.Error!;
            }
        }

        var settlement = await db.Settlements.Include(s => s.Offers).Include(s => s.Installments)
            .SingleOrDefaultAsync(s => s.Id == settlementId && s.LegalCaseId == caseId, cancellationToken);
        if (settlement is null)
        {
            return LitigationErrors.SettlementNotFound;
        }

        var done = action(settlement);
        if (done.IsFailure)
        {
            return done.Error!;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await responses.BuildAsync(caseId, [settlement], cancellationToken))[0];
    }
}

internal sealed class SettlementResponses(ILitigationDbContext db, CaseWrites cases)
{
    public async Task<IReadOnlyList<SettlementResponse>> BuildAsync(Guid caseId, IReadOnlyList<Settlement> settlements, CancellationToken cancellationToken)
    {
        var requested = await RequestedAsync(caseId, cancellationToken);
        var today = cases.Today;
        return [.. settlements.Select(s => ToResponse(s, requested, today))];
    }

    /// <summary>Soma do valor pedido nos pedidos (na primeira instância com valor de cada um) ou, sem eles, o valor da causa.</summary>
    private async Task<decimal?> RequestedAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var valuations = await db.Claims.Where(c => c.LegalCaseId == caseId)
            .SelectMany(c => c.Valuations.Where(v => v.Requested != null).Select(v => new { v.ClaimId, v.Instance, v.Requested }))
            .ToListAsync(cancellationToken);
        if (valuations.Count > 0)
        {
            return valuations.GroupBy(v => v.ClaimId).Sum(g => g.OrderBy(v => v.Instance).First().Requested!.Value);
        }

        return await db.Cases.Where(c => c.Id == caseId).Select(c => c.ClaimValue).SingleOrDefaultAsync(cancellationToken);
    }

    private static SettlementResponse ToResponse(Settlement s, decimal? requested, DateOnly today)
    {
        var compared = s.AgreedAmount ?? s.Offers.OrderBy(o => o.Round).LastOrDefault()?.Amount;
        decimal? difference = requested is { } r && compared is { } c ? r - c : null;
        decimal? percent = difference is { } d && requested is > 0 ? decimal.Round(d / requested.Value * 100m, 2, MidpointRounding.AwayFromZero) : null;
        return new SettlementResponse(
            s.Id,
            s.LegalCaseId,
            s.Direction,
            s.Status,
            s.Notes,
            s.AgreedAmount,
            [.. s.Offers.OrderBy(o => o.Round).Select(o => new SettlementOfferResponse(o.Round, o.Party, o.Amount, o.Terms, o.OfferedOn))],
            [.. s.Installments.OrderBy(i => i.Number).Select(i => new SettlementInstallmentResponse(i.Id, i.Number, i.DueOn, i.Amount, i.PaidOn, i.PaidAmount, i.IsOverdue(today)))],
            requested,
            difference,
            percent,
            s.SubmittedBy,
            s.SubmittedAt,
            s.ApprovedBy,
            s.ApprovedAt,
            s.HomologatedOn,
            s.Reason,
            s.CreatedAt);
    }
}

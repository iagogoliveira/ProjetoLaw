using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Opportunities;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.Proposals;

/// <summary>Proposta enviada pela tela (criação e edição do rascunho).</summary>
public sealed record ProposalInput(
    string Title,
    string? Scope,
    string? Conditions,
    DateOnly? ValidUntil,
    Guid? TemplateId,
    IReadOnlyList<ProposalItemInput>? Items)
{
    public ProposalDetails ToDetails() => new(Title, Scope, Conditions, ValidUntil, TemplateId, Items ?? []);
}

public sealed record ProposalItemResponse(string Description, FeeKind Kind, decimal Quantity, decimal? UnitAmount, decimal? Percentage, decimal? Amount);

public sealed record ProposalResponse(
    Guid Id,
    Guid OpportunityId,
    string Title,
    string? Scope,
    string? Conditions,
    DateOnly? ValidUntil,
    Guid? TemplateId,
    string Currency,
    decimal Total,
    ProposalStatus Status,
    IReadOnlyList<ProposalItemResponse> Items,
    Guid CreatedBy,
    Guid? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? ReturnNote,
    DateTimeOffset? SentAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? DeclinedAt,
    string? DeclineNote,
    DateTimeOffset CreatedAt);

/// <summary>Linha da lista de propostas (com o interessado, para a fila de aprovação).</summary>
public sealed record ProposalSummary(Guid Id, Guid OpportunityId, string ProspectName, string Title, ProposalStatus Status, decimal Total, DateTimeOffset CreatedAt, DateTimeOffset? SentAt);

internal static class ProposalMapping
{
    public static ProposalResponse ToResponse(this Proposal p) => new(
        p.Id,
        p.OpportunityId,
        p.Title,
        p.Scope,
        p.Conditions,
        p.ValidUntil,
        p.TemplateId,
        p.Currency,
        p.Total,
        p.Status,
        [.. p.Items.OrderBy(i => i.Position).Select(i => new ProposalItemResponse(i.Description, i.Kind, i.Quantity, i.UnitAmount, i.Percentage, i.Amount))],
        p.CreatedBy,
        p.SubmittedBy,
        p.SubmittedAt,
        p.ApprovedBy,
        p.ApprovedAt,
        p.ReturnNote,
        p.SentAt,
        p.AcceptedAt,
        p.DeclinedAt,
        p.DeclineNote,
        p.CreatedAt);
}

internal sealed class ProposalInputValidator : AbstractValidator<ProposalInput>
{
    public ProposalInputValidator()
    {
        RuleFor(i => i.Title).NotEmpty().WithErrorCode("crm.title_required")
            .MaximumLength(Proposal.MaxTitleLength).WithErrorCode("crm.title_too_long");
        RuleFor(i => i.Scope).MaximumLength(Proposal.MaxTextLength).WithErrorCode("crm.text_too_long");
        RuleFor(i => i.Conditions).MaximumLength(Proposal.MaxTextLength).WithErrorCode("crm.text_too_long");
        RuleFor(i => i.Items).Must(items => items is null || items.Count <= Proposal.MaxItems).WithErrorCode(CrmErrors.ProposalTooManyItems.Code);
        RuleForEach(i => i.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Description).NotEmpty().WithErrorCode("crm.item_description_required")
                .MaximumLength(Proposal.MaxDescriptionLength).WithErrorCode("crm.item_description_too_long");
            item.RuleFor(x => x.Kind).IsInEnum().WithErrorCode("crm.fee_kind_invalid");
            item.RuleFor(x => x.Percentage).NotNull().InclusiveBetween(0.01m, 100m).When(x => x.Kind == FeeKind.Success)
                .WithErrorCode(CrmErrors.ProposalPercentageInvalid.Code);
            item.RuleFor(x => x.UnitAmount).NotNull().InclusiveBetween(0m, 999_999_999_999m).When(x => x.Kind != FeeKind.Success)
                .WithErrorCode(CrmErrors.ProposalAmountInvalid.Code);
            item.RuleFor(x => x.Quantity).InclusiveBetween(0.01m, 100_000m).When(x => x.Kind != FeeKind.Success)
                .WithErrorCode(CrmErrors.ProposalAmountInvalid.Code);
        });
    }
}

public sealed record CreateProposalCommand(Guid OpportunityId, ProposalInput Proposal) : ICommand<ProposalResponse>;

public sealed record UpdateProposalCommand(Guid Id, ProposalInput Proposal) : ICommand<ProposalResponse>;

internal sealed class CreateProposalValidator : AbstractValidator<CreateProposalCommand>
{
    public CreateProposalValidator() => RuleFor(c => c.Proposal).NotNull().SetValidator(new ProposalInputValidator());
}

internal sealed class UpdateProposalValidator : AbstractValidator<UpdateProposalCommand>
{
    public UpdateProposalValidator() => RuleFor(c => c.Proposal).NotNull().SetValidator(new ProposalInputValidator());
}

internal sealed class CreateProposalHandler(ICrmDbContext db, IIdGenerator ids) : ICommandHandler<CreateProposalCommand, ProposalResponse>
{
    public async Task<Result<ProposalResponse>> Handle(CreateProposalCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(o => o.Id == command.OpportunityId, cancellationToken);
        if (opportunity is null)
        {
            return CrmErrors.OpportunityNotFound;
        }

        if (opportunity.IsClosed)
        {
            return opportunity.Stage == OpportunityStage.Won ? CrmErrors.OpportunityWon : CrmErrors.OpportunityLost;
        }

        if (command.Proposal.TemplateId is { } template && !await db.ProposalTemplates.AnyAsync(t => t.Id == template, cancellationToken))
        {
            return CrmErrors.TemplateNotFound;
        }

        var proposal = Proposal.Create(ids.NewId(), opportunity.Id, command.Proposal.ToDetails());
        if (proposal.IsFailure)
        {
            return proposal.Error;
        }

        db.Proposals.Add(proposal.Value);
        await db.SaveChangesAsync(cancellationToken);
        return proposal.Value.ToResponse();
    }
}

internal sealed class UpdateProposalHandler(ICrmDbContext db) : ICommandHandler<UpdateProposalCommand, ProposalResponse>
{
    public async Task<Result<ProposalResponse>> Handle(UpdateProposalCommand command, CancellationToken cancellationToken)
    {
        var proposal = await db.Proposals.Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (proposal is null)
        {
            return CrmErrors.ProposalNotFound;
        }

        if (command.Proposal.TemplateId is { } template && !await db.ProposalTemplates.AnyAsync(t => t.Id == template, cancellationToken))
        {
            return CrmErrors.TemplateNotFound;
        }

        var updated = proposal.Update(command.Proposal.ToDetails());
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return proposal.ToResponse();
    }
}

/// <summary>Exclui (logicamente) uma proposta ainda não enviada.</summary>
public sealed record DeleteProposalCommand(Guid Id) : ICommand;

internal sealed class DeleteProposalHandler(ICrmDbContext db) : ICommandHandler<DeleteProposalCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteProposalCommand command, CancellationToken cancellationToken)
    {
        var proposal = await db.Proposals.SingleOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (proposal is null)
        {
            return CrmErrors.ProposalNotFound;
        }

        var deletable = proposal.CanDelete();
        if (deletable.IsFailure)
        {
            return deletable.Error;
        }

        db.Proposals.Remove(proposal);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record GetProposalQuery(Guid Id) : IQuery<ProposalResponse>;

internal sealed class GetProposalHandler(ICrmDbContext db) : IQueryHandler<GetProposalQuery, ProposalResponse>
{
    public async Task<Result<ProposalResponse>> Handle(GetProposalQuery query, CancellationToken cancellationToken)
    {
        var proposal = await db.Proposals.AsNoTracking().Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == query.Id, cancellationToken);
        return proposal is null ? CrmErrors.ProposalNotFound : proposal.ToResponse();
    }
}

/// <summary>Posição na lista (mais recentes primeiro).</summary>
public sealed record ProposalsPosition(DateTimeOffset CreatedAt, Guid Id);

public sealed record ProposalsPage(IReadOnlyList<ProposalSummary> Items, ProposalsPosition? Next);

/// <summary>Propostas de uma oportunidade, ou de todas (ex.: a fila "aguardando aprovação"), das mais recentes às mais antigas.</summary>
public sealed record ListProposalsQuery(Guid? OpportunityId, ProposalStatus? Status, ProposalsPosition? After, int Limit) : IQuery<ProposalsPage>;

internal sealed class ListProposalsValidator : AbstractValidator<ListProposalsQuery>
{
    public ListProposalsValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}

internal sealed class ListProposalsHandler(ICrmDbContext db) : IQueryHandler<ListProposalsQuery, ProposalsPage>
{
    public async Task<Result<ProposalsPage>> Handle(ListProposalsQuery query, CancellationToken cancellationToken)
    {
        var proposals = db.Proposals.AsNoTracking();
        if (query.OpportunityId is { } opportunity)
        {
            proposals = proposals.Where(p => p.OpportunityId == opportunity);
        }

        if (query.Status is { } status)
        {
            proposals = proposals.Where(p => p.Status == status);
        }

        if (query.After is { } after)
        {
            proposals = proposals.Where(p => p.CreatedAt < after.CreatedAt || (p.CreatedAt == after.CreatedAt && p.Id.CompareTo(after.Id) < 0));
        }

        var page = await proposals
            .Join(db.Opportunities, p => p.OpportunityId, o => o.Id, (p, o) => new { p, o.ProspectName })
            .OrderByDescending(x => x.p.CreatedAt).ThenByDescending(x => x.p.Id).Take(query.Limit + 1)
            .Select(x => new ProposalSummary(x.p.Id, x.p.OpportunityId, x.ProspectName, x.p.Title, x.p.Status, x.p.Total, x.p.CreatedAt, x.p.SentAt))
            .ToListAsync(cancellationToken);
        var last = page.Count > query.Limit ? page[query.Limit - 1] : null;
        return new ProposalsPage([.. page.Take(query.Limit)], last is null ? null : new ProposalsPosition(last.CreatedAt, last.Id));
    }
}

/// <summary>Passos do fluxo de aprovação e envio (decisão de 2026-10-17: aprovação interna sempre).</summary>
public enum ProposalStep
{
    Submit,
    Approve,
    ReturnToDraft,
    Decline,
}

/// <summary>Envia para aprovação, aprova, devolve para rascunho ou registra a recusa do cliente.</summary>
public sealed record ChangeProposalCommand(Guid Id, ProposalStep Step, string? Note) : ICommand<ProposalResponse>;

internal sealed class ChangeProposalValidator : AbstractValidator<ChangeProposalCommand>
{
    public ChangeProposalValidator() => RuleFor(c => c.Note).MaximumLength(Proposal.MaxNoteLength).WithErrorCode("crm.note_too_long");
}

internal sealed class ChangeProposalHandler(ICrmDbContext db, ICurrentUser user, IOrganizationMembers members, IClock clock)
    : ICommandHandler<ChangeProposalCommand, ProposalResponse>
{
    public async Task<Result<ProposalResponse>> Handle(ChangeProposalCommand command, CancellationToken cancellationToken)
    {
        var proposal = await db.Proposals.Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (proposal is null)
        {
            return CrmErrors.ProposalNotFound;
        }

        var userId = user.UserId ?? throw new InvalidOperationException("Proposta alterada sem usuário autenticado.");
        var changed = command.Step switch
        {
            ProposalStep.Submit => proposal.Submit(userId, clock.UtcNow),
            ProposalStep.Approve => proposal.Approve(userId, clock.UtcNow, await members.CountActiveAsync(cancellationToken) == 1),
            ProposalStep.ReturnToDraft => proposal.ReturnToDraft(command.Note),
            _ => proposal.Decline(command.Note, clock.UtcNow),
        };
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return proposal.ToResponse();
    }
}

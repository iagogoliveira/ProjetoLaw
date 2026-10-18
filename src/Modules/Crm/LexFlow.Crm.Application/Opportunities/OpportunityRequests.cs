using System.Text.Json.Nodes;
using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Application.Conflicts;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Conflicts;
using LexFlow.Crm.Domain.Opportunities;
using LexFlow.Crm.Domain.People;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.Opportunities;

/// <summary>Oportunidade enviada pela tela (criação e edição).</summary>
public sealed record OpportunityInput(
    string Title,
    Guid? PersonId,
    ProspectInput? Prospect,
    Guid? OriginId,
    decimal? EstimatedValue,
    DateOnly? ExpectedCloseDate,
    Guid? ResponsibleUserId,
    string? Notes,
    JsonObject? CustomFields);

public sealed record ProspectResponse(PersonKind Kind, string Name, string? Document, string? Email, string? Phone);

public sealed record OpportunityResponse(
    Guid Id,
    string Title,
    Guid? PersonId,
    ProspectResponse Prospect,
    Guid? OriginId,
    decimal? EstimatedValue,
    DateOnly? ExpectedCloseDate,
    Guid? ResponsibleUserId,
    string? Notes,
    OpportunityStage Stage,
    DateTimeOffset StageChangedAt,
    Guid? LossReasonId,
    string? LossNote,
    Guid? ConflictCheckId,
    DateTimeOffset? WonAt,
    JsonObject CustomFields,
    DateTimeOffset CreatedAt);

/// <summary>Cartão do kanban.</summary>
public sealed record OpportunitySummary(
    Guid Id,
    string Title,
    string ProspectName,
    Guid? PersonId,
    OpportunityStage Stage,
    decimal? EstimatedValue,
    DateOnly? ExpectedCloseDate,
    Guid? ResponsibleUserId,
    Guid? OriginId,
    DateTimeOffset StageChangedAt);

internal static class OpportunityMapping
{
    public static OpportunityResponse ToResponse(this Opportunity o) => new(
        o.Id,
        o.Title,
        o.PersonId,
        new ProspectResponse(o.ProspectKind, o.ProspectName, o.ProspectDocument, o.ProspectEmail, o.ProspectPhone),
        o.OriginId,
        o.EstimatedValue,
        o.ExpectedCloseDate,
        o.ResponsibleUserId,
        o.Notes,
        o.Stage,
        o.StageChangedAt,
        o.LossReasonId,
        o.LossNote,
        o.ConflictCheckId,
        o.WonAt,
        JsonNode.Parse(o.CustomFields)?.AsObject() ?? [],
        o.CreatedAt);
}

internal sealed class OpportunityInputValidator : AbstractValidator<OpportunityInput>
{
    public OpportunityInputValidator()
    {
        RuleFor(i => i.Title).NotEmpty().WithErrorCode("crm.title_required")
            .MaximumLength(Opportunity.MaxTitleLength).WithErrorCode("crm.title_too_long");
        RuleFor(i => i.Prospect).NotNull().When(i => i.PersonId is null).WithErrorCode("crm.prospect_required");
        RuleFor(i => i.Prospect!).ChildRules(p =>
        {
            p.RuleFor(x => x.Kind).IsInEnum().WithErrorCode("crm.kind_invalid");
            p.RuleFor(x => x.Name).NotEmpty().WithErrorCode("crm.name_required")
                .MaximumLength(Person.MaxNameLength).WithErrorCode("crm.name_too_long");
            p.RuleFor(x => x.Email).Must(e => string.IsNullOrWhiteSpace(e) || Email.IsValid(e)).WithErrorCode("crm.email_invalid");
            p.RuleFor(x => x.Phone).Must(v => string.IsNullOrWhiteSpace(v) || Phone.IsValid(v)).WithErrorCode("crm.phone_invalid");
        }).When(i => i.PersonId is null && i.Prospect is not null);
        RuleFor(i => i.EstimatedValue).GreaterThanOrEqualTo(0).WithErrorCode(CrmErrors.OpportunityValueInvalid.Code)
            .LessThan(1_000_000_000_000m).WithErrorCode(CrmErrors.OpportunityValueInvalid.Code);
        RuleFor(i => i.Notes).MaximumLength(Opportunity.MaxNotesLength).WithErrorCode("crm.notes_too_long");
    }
}

/// <summary>
/// Regras comuns à criação e à edição: o interessado (cópia da pessoa cadastrada, quando informada), a origem da
/// tabela auxiliar, o responsável membro do escritório e os campos customizados.
/// </summary>
internal sealed class OpportunityRules(ICrmDbContext db, ICustomFieldValidator customFields, ILookupItemValidator lookups, IMembershipLookup memberships, ITenantContext tenant)
{
    public async Task<Result<(OpportunityDetails Details, string CustomFields)>> CheckAsync(OpportunityInput input, CancellationToken cancellationToken)
    {
        var prospect = input.Prospect;
        if (input.PersonId is { } personId)
        {
            var person = await db.People.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, cancellationToken);
            if (person is null)
            {
                return CrmErrors.PersonNotFound;
            }

            if (person.IsAnonymized)
            {
                return CrmErrors.PersonAnonymized;
            }

            prospect = new ProspectInput(person.Kind, person.Name, person.Document, person.Email, person.Phone);
        }

        if (input.OriginId is { } origin && !await lookups.IsActiveItemAsync(SystemLookupTables.OpportunityOrigins, origin, cancellationToken))
        {
            return CrmErrors.OriginNotFound;
        }

        if (input.ResponsibleUserId is { } responsible
            && !(await memberships.GetAsync(responsible, cancellationToken)).OrganizationIds.Contains(tenant.RequiredOrganizationId))
        {
            return CrmErrors.ResponsibleNotMember;
        }

        var validation = await customFields.ValidateAsync(CustomFieldEntities.Opportunities, input.CustomFields, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var details = new OpportunityDetails(input.Title, input.PersonId, prospect!, input.OriginId, input.EstimatedValue, input.ExpectedCloseDate, input.ResponsibleUserId, input.Notes);
        return (details, validation.Values.ToJsonString());
    }
}

public sealed record CreateOpportunityCommand(OpportunityInput Opportunity) : ICommand<OpportunityResponse>;

public sealed record UpdateOpportunityCommand(Guid Id, OpportunityInput Opportunity) : ICommand<OpportunityResponse>;

internal sealed class CreateOpportunityValidator : AbstractValidator<CreateOpportunityCommand>
{
    public CreateOpportunityValidator() => RuleFor(c => c.Opportunity).NotNull().SetValidator(new OpportunityInputValidator());
}

internal sealed class UpdateOpportunityValidator : AbstractValidator<UpdateOpportunityCommand>
{
    public UpdateOpportunityValidator() => RuleFor(c => c.Opportunity).NotNull().SetValidator(new OpportunityInputValidator());
}

internal sealed class CreateOpportunityHandler(ICrmDbContext db, OpportunityRules rules, IIdGenerator ids, IClock clock)
    : ICommandHandler<CreateOpportunityCommand, OpportunityResponse>
{
    public async Task<Result<OpportunityResponse>> Handle(CreateOpportunityCommand command, CancellationToken cancellationToken)
    {
        var checkedInput = await rules.CheckAsync(command.Opportunity, cancellationToken);
        if (checkedInput.IsFailure)
        {
            return checkedInput.Error;
        }

        var opportunity = Opportunity.Create(ids.NewId(), checkedInput.Value.Details, clock.UtcNow);
        if (opportunity.IsFailure)
        {
            return opportunity.Error;
        }

        opportunity.Value.SetCustomFields(checkedInput.Value.CustomFields);
        db.Opportunities.Add(opportunity.Value);
        await db.SaveChangesAsync(cancellationToken);
        return opportunity.Value.ToResponse();
    }
}

internal sealed class UpdateOpportunityHandler(ICrmDbContext db, OpportunityRules rules) : ICommandHandler<UpdateOpportunityCommand, OpportunityResponse>
{
    public async Task<Result<OpportunityResponse>> Handle(UpdateOpportunityCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities.SingleOrDefaultAsync(o => o.Id == command.Id, cancellationToken);
        if (opportunity is null)
        {
            return CrmErrors.OpportunityNotFound;
        }

        var checkedInput = await rules.CheckAsync(command.Opportunity, cancellationToken);
        if (checkedInput.IsFailure)
        {
            return checkedInput.Error;
        }

        var updated = opportunity.Update(checkedInput.Value.Details);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        opportunity.SetCustomFields(checkedInput.Value.CustomFields);
        await db.SaveChangesAsync(cancellationToken);
        return opportunity.ToResponse();
    }
}

/// <summary>Exclusão lógica (a oportunidade ganha fica, porque originou um cliente); as propostas abertas também saem.</summary>
public sealed record DeleteOpportunityCommand(Guid Id) : ICommand;

internal sealed class DeleteOpportunityHandler(ICrmDbContext db) : ICommandHandler<DeleteOpportunityCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteOpportunityCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities.SingleOrDefaultAsync(o => o.Id == command.Id, cancellationToken);
        if (opportunity is null)
        {
            return CrmErrors.OpportunityNotFound;
        }

        if (opportunity.Stage == OpportunityStage.Won)
        {
            return CrmErrors.OpportunityWon;
        }

        db.Proposals.RemoveRange(await db.Proposals.Where(p => p.OpportunityId == opportunity.Id).ToListAsync(cancellationToken));
        db.Opportunities.Remove(opportunity);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record GetOpportunityQuery(Guid Id) : IQuery<OpportunityResponse>;

internal sealed class GetOpportunityHandler(ICrmDbContext db) : IQueryHandler<GetOpportunityQuery, OpportunityResponse>
{
    public async Task<Result<OpportunityResponse>> Handle(GetOpportunityQuery query, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(o => o.Id == query.Id, cancellationToken);
        return opportunity is null ? CrmErrors.OpportunityNotFound : opportunity.ToResponse();
    }
}

/// <summary>Posição na lista (mais recentes primeiro).</summary>
public sealed record OpportunitiesPosition(DateTimeOffset CreatedAt, Guid Id);

public sealed record OpportunitiesPage(IReadOnlyList<OpportunitySummary> Items, OpportunitiesPosition? Next);

/// <summary>Cartões do kanban, por etapa e responsável, das oportunidades mais recentes às mais antigas.</summary>
public sealed record ListOpportunitiesQuery(OpportunityStage? Stage, Guid? ResponsibleUserId, OpportunitiesPosition? After, int Limit) : IQuery<OpportunitiesPage>;

internal sealed class ListOpportunitiesValidator : AbstractValidator<ListOpportunitiesQuery>
{
    public ListOpportunitiesValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}

internal sealed class ListOpportunitiesHandler(ICrmDbContext db) : IQueryHandler<ListOpportunitiesQuery, OpportunitiesPage>
{
    public async Task<Result<OpportunitiesPage>> Handle(ListOpportunitiesQuery query, CancellationToken cancellationToken)
    {
        var opportunities = db.Opportunities.AsNoTracking();
        if (query.Stage is { } stage)
        {
            opportunities = opportunities.Where(o => o.Stage == stage);
        }

        if (query.ResponsibleUserId is { } responsible)
        {
            opportunities = opportunities.Where(o => o.ResponsibleUserId == responsible);
        }

        if (query.After is { } after)
        {
            opportunities = opportunities.Where(o => o.CreatedAt < after.CreatedAt || (o.CreatedAt == after.CreatedAt && o.Id.CompareTo(after.Id) < 0));
        }

        var page = await opportunities.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id).Take(query.Limit + 1)
            .Select(o => new
            {
                Summary = new OpportunitySummary(o.Id, o.Title, o.ProspectName, o.PersonId, o.Stage, o.EstimatedValue, o.ExpectedCloseDate, o.ResponsibleUserId, o.OriginId, o.StageChangedAt),
                o.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        var last = page.Count > query.Limit ? page[query.Limit - 1] : null;
        return new OpportunitiesPage([.. page.Take(query.Limit).Select(p => p.Summary)], last is null ? null : new OpportunitiesPosition(last.CreatedAt, last.Summary.Id));
    }
}

/// <summary>Cabeçalho de uma coluna do kanban: quantas oportunidades e o valor estimado somado.</summary>
public sealed record OpportunityStageSummary(OpportunityStage Stage, int Count, decimal EstimatedValue);

public sealed record OpportunitiesSummaryQuery(Guid? ResponsibleUserId) : IQuery<IReadOnlyList<OpportunityStageSummary>>;

internal sealed class OpportunitiesSummaryHandler(ICrmDbContext db) : IQueryHandler<OpportunitiesSummaryQuery, IReadOnlyList<OpportunityStageSummary>>
{
    public async Task<Result<IReadOnlyList<OpportunityStageSummary>>> Handle(OpportunitiesSummaryQuery query, CancellationToken cancellationToken)
    {
        var opportunities = db.Opportunities.AsNoTracking();
        if (query.ResponsibleUserId is { } responsible)
        {
            opportunities = opportunities.Where(o => o.ResponsibleUserId == responsible);
        }

        var totals = await opportunities.GroupBy(o => o.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count(), Value = g.Sum(o => o.EstimatedValue ?? 0m) })
            .ToListAsync(cancellationToken);
        return Enum.GetValues<OpportunityStage>()
            .Select(stage => totals.SingleOrDefault(t => t.Stage == stage) is { } total
                ? new OpportunityStageSummary(stage, total.Count, total.Value)
                : new OpportunityStageSummary(stage, 0, 0m))
            .ToList();
    }
}

/// <summary>Arrasta o cartão entre lead, reunião e proposta (ou reabre uma oportunidade perdida).</summary>
public sealed record MoveOpportunityCommand(Guid Id, OpportunityStage Stage) : ICommand<OpportunityResponse>;

internal sealed class MoveOpportunityValidator : AbstractValidator<MoveOpportunityCommand>
{
    public MoveOpportunityValidator() => RuleFor(c => c.Stage).IsInEnum().WithErrorCode(CrmErrors.OpportunityStageInvalid.Code);
}

internal sealed class MoveOpportunityHandler(ICrmDbContext db, IClock clock) : ICommandHandler<MoveOpportunityCommand, OpportunityResponse>
{
    public async Task<Result<OpportunityResponse>> Handle(MoveOpportunityCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities.SingleOrDefaultAsync(o => o.Id == command.Id, cancellationToken);
        if (opportunity is null)
        {
            return CrmErrors.OpportunityNotFound;
        }

        var moved = opportunity.MoveTo(command.Stage, clock.UtcNow);
        if (moved.IsFailure)
        {
            return moved.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return opportunity.ToResponse();
    }
}

/// <summary>Marca a oportunidade como perdida, com o motivo (tabela auxiliar "Motivos de perda") e uma observação.</summary>
public sealed record LoseOpportunityCommand(Guid Id, Guid ReasonId, string? Note) : ICommand<OpportunityResponse>;

internal sealed class LoseOpportunityValidator : AbstractValidator<LoseOpportunityCommand>
{
    public LoseOpportunityValidator()
    {
        RuleFor(c => c.ReasonId).NotEmpty().WithErrorCode("crm.loss_reason_required");
        RuleFor(c => c.Note).MaximumLength(Opportunity.MaxLossNoteLength).WithErrorCode("crm.loss_note_too_long");
    }
}

internal sealed class LoseOpportunityHandler(ICrmDbContext db, ILookupItemValidator lookups, IClock clock) : ICommandHandler<LoseOpportunityCommand, OpportunityResponse>
{
    public async Task<Result<OpportunityResponse>> Handle(LoseOpportunityCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities.SingleOrDefaultAsync(o => o.Id == command.Id, cancellationToken);
        if (opportunity is null)
        {
            return CrmErrors.OpportunityNotFound;
        }

        if (!await lookups.IsActiveItemAsync(SystemLookupTables.LossReasons, command.ReasonId, cancellationToken))
        {
            return CrmErrors.LossReasonNotFound;
        }

        var lost = opportunity.Lose(command.ReasonId, command.Note, clock.UtcNow);
        if (lost.IsFailure)
        {
            return lost.Error;
        }

        foreach (var proposal in await db.Proposals.Where(p => p.OpportunityId == opportunity.Id).ToListAsync(cancellationToken))
        {
            proposal.Supersede();
        }

        await db.SaveChangesAsync(cancellationToken);
        return opportunity.ToResponse();
    }
}

/// <summary>
/// Verificação de conflito (RF-07) com os dados do interessado, do lado Cliente, ligada à oportunidade. A aceitação
/// da proposta exige que ela tenha sido prosseguida (<c>POST /conflict-checks/{id}/proceed</c>).
/// </summary>
public sealed record CheckOpportunityConflictCommand(Guid Id) : ICommand<ConflictCheckResponse>;

internal sealed class CheckOpportunityConflictHandler(ICrmDbContext db, ConflictDetector detector, IIdGenerator ids)
    : ICommandHandler<CheckOpportunityConflictCommand, ConflictCheckResponse>
{
    public async Task<Result<ConflictCheckResponse>> Handle(CheckOpportunityConflictCommand command, CancellationToken cancellationToken)
    {
        var opportunity = await db.Opportunities.SingleOrDefaultAsync(o => o.Id == command.Id, cancellationToken);
        if (opportunity is null)
        {
            return CrmErrors.OpportunityNotFound;
        }

        var emails = opportunity.ProspectEmail is { } email ? [email] : Array.Empty<string>();
        var hits = await detector.DetectAsync(opportunity.ProspectName, opportunity.ProspectDocument, ConflictSide.Client, emails, cancellationToken);
        var check = ConflictCheck.Record(ids.NewId(), opportunity.ProspectName, opportunity.ProspectDocument, ConflictSide.Client, hits);
        var linked = opportunity.LinkConflictCheck(check.Id);
        if (linked.IsFailure)
        {
            return linked.Error;
        }

        db.ConflictChecks.Add(check);
        await db.SaveChangesAsync(cancellationToken);
        return check.ToResponse();
    }
}

using System.Text.Json.Nodes;
using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Crm.Contracts;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Domain.Numbering;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Cases;

/// <summary>Processo ou caso enviado pela tela (criação e edição).</summary>
public sealed record CaseInput(
    string? CnjNumber,
    Guid? CourtId,
    CaseInstance? Instance,
    string? District,
    string? CourtUnit,
    string? Rite,
    string? ProceduralClass,
    string? Subject,
    Guid? ActionTypeId,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? PhaseId,
    decimal? ClaimValue,
    DateOnly? FiledOn,
    DateOnly? ServedOn,
    string? Description,
    Guid? PayerPersonId,
    IReadOnlyList<PartyInput>? Parties,
    IReadOnlyList<AssignmentInput>? Assignments,
    JsonObject? CustomFields);

public sealed record PartyResponse(Guid PersonId, string Name, string? Document, PartyPole Pole, bool IsClient, string? CounselName, string? CounselOab);

public sealed record AssignmentResponse(AssignmentRole Role, Guid? UserId, Guid? TeamId);

public sealed record CourtResponse(Guid Id, string Acronym, string Name, string? State);

/// <summary>Processo ligado na hierarquia (o principal, ou um incidente, recurso, cumprimento ou apenso).</summary>
public sealed record RelatedCaseResponse(Guid RelationId, Guid CaseId, string InternalFolder, string? CnjNumber, RelationKind Kind);

public sealed record CaseResponse(
    Guid Id,
    CaseKind Kind,
    string InternalFolder,
    string? CnjNumber,
    CourtResponse? Court,
    CaseInstance? Instance,
    string? District,
    string? CourtUnit,
    string? Rite,
    string? ProceduralClass,
    string? Subject,
    Guid? ActionTypeId,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? PhaseId,
    CaseStatus Status,
    decimal? ClaimValue,
    DateOnly? FiledOn,
    DateOnly? ServedOn,
    string? Description,
    Guid? PayerPersonId,
    IReadOnlyList<PartyResponse> Parties,
    IReadOnlyList<AssignmentResponse> Assignments,
    RelatedCaseResponse? Parent,
    IReadOnlyList<RelatedCaseResponse> Children,
    DateOnly? ClosedOn,
    Guid? ClosingReasonId,
    CaseOutcome? Outcome,
    decimal? FinalAmount,
    string? ClosingNote,
    JsonObject CustomFields,
    DateTimeOffset CreatedAt);

/// <summary>O número CNJ com a pontuação (<c>NNNNNNN-DD.AAAA.J.TR.OOOO</c>).</summary>
internal static class CnjFormat
{
    public static string? Of(string? digits) =>
        digits is null ? null : SharedKernel.ValueObjects.CnjNumber.Create(digits) is { IsSuccess: true } cnj ? cnj.Value.Formatted : digits;
}

/// <summary>Monta a resposta com os nomes das partes (Crm), o tribunal (Platform) e a hierarquia.</summary>
internal sealed class CaseResponseBuilder(ILitigationDbContext db, IPeopleDirectory people, ICourtDirectory courts)
{
    public async Task<CaseResponse> BuildAsync(LegalCase c, CancellationToken cancellationToken)
    {
        var cards = (await people.GetAsync([.. c.Parties.Select(p => p.PersonId)], cancellationToken)).ToDictionary(p => p.Id);
        var court = c.CourtId is { } courtId ? await courts.GetAsync(courtId, cancellationToken) : null;
        var relations = await db.Relations.AsNoTracking()
            .Where(r => r.ParentCaseId == c.Id || r.ChildCaseId == c.Id)
            .Join(db.Cases, r => r.ParentCaseId == c.Id ? r.ChildCaseId : r.ParentCaseId, other => other.Id, (r, other) => new { r, other.Id, other.InternalFolder, other.CnjNumber })
            .ToListAsync(cancellationToken);
        var parent = relations.SingleOrDefault(x => x.r.ChildCaseId == c.Id);

        return new CaseResponse(
            c.Id,
            c.Kind,
            c.InternalFolder,
            CnjFormat.Of(c.CnjNumber),
            court is null ? null : new CourtResponse(court.Id, court.Acronym, court.Name, court.State),
            c.Instance,
            c.District,
            c.CourtUnit,
            c.Rite,
            c.ProceduralClass,
            c.Subject,
            c.ActionTypeId,
            c.PracticeAreaId,
            c.BranchId,
            c.PhaseId,
            c.Status,
            c.ClaimValue,
            c.FiledOn,
            c.ServedOn,
            c.Description,
            c.PayerPersonId,
            [.. c.Parties.Select(p => new PartyResponse(
                p.PersonId,
                cards.TryGetValue(p.PersonId, out var card) ? card.Name : "—",
                cards.TryGetValue(p.PersonId, out var withDocument) ? withDocument.Document : null,
                p.Pole,
                p.IsClient,
                p.CounselName,
                p.CounselOab))],
            [.. c.Assignments.Select(a => new AssignmentResponse(a.Role, a.UserId, a.TeamId))],
            parent is null ? null : new RelatedCaseResponse(parent.r.Id, parent.Id, parent.InternalFolder, CnjFormat.Of(parent.CnjNumber), parent.r.Kind),
            [.. relations.Where(x => x.r.ParentCaseId == c.Id).Select(x => new RelatedCaseResponse(x.r.Id, x.Id, x.InternalFolder, CnjFormat.Of(x.CnjNumber), x.r.Kind))],
            c.ClosedOn,
            c.ClosingReasonId,
            c.Outcome,
            c.FinalAmount,
            c.ClosingNote,
            JsonNode.Parse(c.CustomFields)?.AsObject() ?? [],
            c.CreatedAt);
    }
}

internal sealed class CaseInputValidator : AbstractValidator<CaseInput>
{
    public CaseInputValidator()
    {
        RuleFor(i => i.Instance).IsInEnum().When(i => i.Instance is not null).WithErrorCode("litigation.instance_invalid");
        RuleFor(i => i.District).MaximumLength(LegalCase.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(i => i.CourtUnit).MaximumLength(LegalCase.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(i => i.Rite).MaximumLength(LegalCase.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(i => i.ProceduralClass).MaximumLength(LegalCase.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(i => i.Subject).MaximumLength(LegalCase.MaxTextLength).WithErrorCode("litigation.text_too_long");
        RuleFor(i => i.Description).MaximumLength(LegalCase.MaxDescriptionLength).WithErrorCode("litigation.text_too_long");
        RuleFor(i => i.ClaimValue).GreaterThanOrEqualTo(0).WithErrorCode(LitigationErrors.ValueInvalid.Code)
            .LessThan(1_000_000_000_000_000m).WithErrorCode(LitigationErrors.ValueInvalid.Code);
        RuleFor(i => i.Parties).NotEmpty().WithErrorCode(LitigationErrors.ClientRequired.Code);
        RuleForEach(i => i.Parties).ChildRules(p =>
        {
            p.RuleFor(x => x.PersonId).NotEmpty().WithErrorCode(LitigationErrors.PersonNotFound.Code);
            p.RuleFor(x => x.Pole).IsInEnum().WithErrorCode("litigation.pole_invalid");
            p.RuleFor(x => x.CounselName).MaximumLength(CaseParty.MaxCounselLength).WithErrorCode("litigation.text_too_long");
            p.RuleFor(x => x.CounselOab).MaximumLength(20).WithErrorCode("litigation.text_too_long");
        });
        RuleForEach(i => i.Assignments).ChildRules(a => a.RuleFor(x => x.Role).IsInEnum().WithErrorCode(LitigationErrors.AssignmentInvalid.Code));
    }
}

/// <summary>
/// Regras comuns à criação e à edição, que dependem de outros módulos: número CNJ único no escritório e o tribunal
/// (pelo CNJ, quando não informado), pessoas do Crm, área, filial e equipes do Organization, responsáveis membros,
/// fase e tipo de ação das tabelas auxiliares, e os campos customizados.
/// </summary>
internal sealed class CaseRules(
    ILitigationDbContext db,
    IPeopleDirectory people,
    ICourtDirectory courts,
    IOrganizationDirectory organization,
    IMembershipLookup memberships,
    ILookupItemValidator lookups,
    ICustomFieldValidator customFields,
    ITenantContext tenant)
{
    public async Task<Result<(CaseDetails Details, string CustomFields)>> CheckAsync(CaseKind kind, CaseInput input, LegalCase? existing, CancellationToken cancellationToken)
    {
        var cnj = LegalCase.NormalizeCnj(kind, input.CnjNumber);
        if (cnj.IsFailure)
        {
            return cnj.Error;
        }

        var court = await CourtAsync(input.CourtId, cnj.Value, cancellationToken);
        if (court.IsFailure)
        {
            return court.Error;
        }

        if (cnj.Value is { } digits && await db.Cases.AnyAsync(c => c.CnjNumber == digits && c.Id != (existing == null ? Guid.Empty : existing.Id), cancellationToken))
        {
            return LitigationErrors.CnjTaken;
        }

        var references = await ReferencesAsync(input, existing, cancellationToken);
        if (references.IsFailure)
        {
            return references.Error;
        }

        var validation = await customFields.ValidateAsync(CustomFieldEntities.Cases, input.CustomFields, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToValidationError();
        }

        var details = new CaseDetails(
            input.CnjNumber, court.Value, input.Instance, input.District, input.CourtUnit, input.Rite, input.ProceduralClass, input.Subject,
            input.ActionTypeId, input.PracticeAreaId, input.BranchId, input.PhaseId, input.ClaimValue, input.FiledOn, input.ServedOn,
            input.Description, input.PayerPersonId, input.Parties ?? [], input.Assignments ?? []);
        return (details, validation.Values.ToJsonString());
    }

    /// <summary>O tribunal informado (conferido) ou o do número CNJ (segmento J e código TR), quando existir.</summary>
    private async Task<Result<Guid?>> CourtAsync(Guid? courtId, string? cnj, CancellationToken cancellationToken)
    {
        if (courtId is { } id)
        {
            return await courts.GetAsync(id, cancellationToken) is null ? LitigationErrors.CourtNotFound : Result.Ok<Guid?>(id);
        }

        if (cnj is null)
        {
            return Result.Ok<Guid?>(null);
        }

        var number = SharedKernel.ValueObjects.CnjNumber.Create(cnj).Value;
        return Result.Ok((await courts.FindAsync(number.Segment, number.Court, cancellationToken))?.Id);
    }

    private async Task<Result> ReferencesAsync(CaseInput input, LegalCase? existing, CancellationToken cancellationToken)
    {
        var parties = input.Parties ?? [];
        var cards = (await people.GetAsync([.. parties.Select(p => p.PersonId).Distinct()], cancellationToken)).ToDictionary(p => p.Id);
        var current = existing?.Parties.Select(p => p.PersonId).ToHashSet() ?? [];
        foreach (var party in parties)
        {
            if (!cards.TryGetValue(party.PersonId, out var card))
            {
                return LitigationErrors.PersonNotFound;
            }

            if (card.IsAnonymized && !current.Contains(party.PersonId))
            {
                return LitigationErrors.PersonAnonymized;
            }
        }

        if (input.PracticeAreaId is { } area && (await organization.GetAsync(OrgUnitKind.PracticeArea, [area], cancellationToken)).Count == 0)
        {
            return LitigationErrors.PracticeAreaNotFound;
        }

        if (input.BranchId is { } branch && (await organization.GetAsync(OrgUnitKind.Branch, [branch], cancellationToken)).Count == 0)
        {
            return LitigationErrors.BranchNotFound;
        }

        var assignments = input.Assignments ?? [];
        var teams = assignments.Where(a => a.TeamId is not null).Select(a => a.TeamId!.Value).Distinct().ToList();
        if (teams.Count > 0 && (await organization.GetAsync(OrgUnitKind.Team, teams, cancellationToken)).Count != teams.Count)
        {
            return LitigationErrors.TeamNotFound;
        }

        foreach (var user in assignments.Where(a => a.UserId is not null).Select(a => a.UserId!.Value).Distinct())
        {
            if (!(await memberships.GetAsync(user, cancellationToken)).OrganizationIds.Contains(tenant.RequiredOrganizationId))
            {
                return LitigationErrors.ResponsibleNotMember;
            }
        }

        if (input.PhaseId is { } phase && !await lookups.IsActiveItemAsync(SystemLookupTables.CasePhases, phase, cancellationToken))
        {
            return LitigationErrors.PhaseNotFound;
        }

        if (input.ActionTypeId is { } actionType && !await lookups.IsActiveItemAsync(SystemLookupTables.ActionTypes, actionType, cancellationToken))
        {
            return LitigationErrors.ActionTypeNotFound;
        }

        return Result.Success;
    }
}

/// <summary>
/// Cadastro de processo ou caso (Etapa 5, tarefas 1 a 3). A pasta interna sai da numeração do escritório: a sigla da
/// área, o ano do cadastro e o próximo número do contador da área naquele ano.
/// </summary>
public sealed record CreateCaseCommand(CaseKind Kind, CaseInput Case) : ICommand<CaseResponse>;

public sealed record UpdateCaseCommand(Guid Id, CaseInput Case) : ICommand<CaseResponse>;

internal sealed class CreateCaseValidator : AbstractValidator<CreateCaseCommand>
{
    public CreateCaseValidator()
    {
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("litigation.kind_invalid");
        RuleFor(c => c.Case).NotNull().SetValidator(new CaseInputValidator());
    }
}

internal sealed class UpdateCaseValidator : AbstractValidator<UpdateCaseCommand>
{
    public UpdateCaseValidator() => RuleFor(c => c.Case).NotNull().SetValidator(new CaseInputValidator());
}

internal sealed class CreateCaseHandler(
    ILitigationDbContext db,
    CaseRules rules,
    ICaseCounter counter,
    IOrganizationDirectory organization,
    CaseResponseBuilder responses,
    IIdGenerator ids,
    IClock clock) : ICommandHandler<CreateCaseCommand, CaseResponse>
{
    public async Task<Result<CaseResponse>> Handle(CreateCaseCommand command, CancellationToken cancellationToken)
    {
        var checkedInput = await rules.CheckAsync(command.Kind, command.Case, null, cancellationToken);
        if (checkedInput.IsFailure)
        {
            return checkedInput.Error;
        }

        var folder = await FolderAsync(command.Case.PracticeAreaId, cancellationToken);
        var legalCase = LegalCase.Create(ids.NewId(), command.Kind, folder, checkedInput.Value.Details);
        if (legalCase.IsFailure)
        {
            return legalCase.Error;
        }

        legalCase.Value.SetCustomFields(checkedInput.Value.CustomFields);
        db.Cases.Add(legalCase.Value);
        await db.SaveChangesAsync(cancellationToken);
        return await responses.BuildAsync(legalCase.Value, cancellationToken);
    }

    private async Task<string> FolderAsync(Guid? areaId, CancellationToken cancellationToken)
    {
        var numbering = await db.Numberings.AsNoTracking().FirstOrDefaultAsync(cancellationToken) ?? CaseNumbering.Default(Guid.Empty);
        var areaName = areaId is { } area ? (await organization.GetAsync(OrgUnitKind.PracticeArea, [area], cancellationToken)).SingleOrDefault()?.Name : null;
        var year = clock.Today.Year;
        var sequence = await counter.NextAsync(areaId, year, cancellationToken);
        return numbering.Render(numbering.CodeOf(areaId, areaName), year, sequence);
    }
}

internal sealed class UpdateCaseHandler(ILitigationDbContext db, CaseRules rules, CaseResponseBuilder responses) : ICommandHandler<UpdateCaseCommand, CaseResponse>
{
    public async Task<Result<CaseResponse>> Handle(UpdateCaseCommand command, CancellationToken cancellationToken)
    {
        var legalCase = await db.Cases.Include(c => c.Parties).Include(c => c.Assignments).SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (legalCase is null)
        {
            return LitigationErrors.CaseNotFound;
        }

        var checkedInput = await rules.CheckAsync(legalCase.Kind, command.Case, legalCase, cancellationToken);
        if (checkedInput.IsFailure)
        {
            return checkedInput.Error;
        }

        var updated = legalCase.Update(checkedInput.Value.Details);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        legalCase.SetCustomFields(checkedInput.Value.CustomFields);
        await db.SaveChangesAsync(cancellationToken);
        return await responses.BuildAsync(legalCase, cancellationToken);
    }
}

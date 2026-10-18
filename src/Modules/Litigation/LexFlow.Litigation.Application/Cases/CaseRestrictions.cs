using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Cases;

/// <summary>Um usuário ou uma equipe numa das listas do caso sigiloso.</summary>
public sealed record RestrictionEntry(Guid? UserId, Guid? TeamId);

/// <summary>As listas do caso sigiloso: permitidos (se houver, só eles veem) e bloqueados (nunca veem).</summary>
public sealed record CaseRestrictionsResponse(IReadOnlyList<RestrictionEntry> Allowed, IReadOnlyList<RestrictionEntry> Blocked);

public sealed record GetCaseRestrictionsQuery(Guid CaseId) : IQuery<CaseRestrictionsResponse>;

internal static class RestrictionMapping
{
    public static CaseRestrictionsResponse ToResponse(this LegalCase c) => new(
        [.. c.Restrictions.Where(r => r.Mode == RestrictionMode.Allow).Select(r => new RestrictionEntry(r.UserId, r.TeamId))],
        [.. c.Restrictions.Where(r => r.Mode == RestrictionMode.Block).Select(r => new RestrictionEntry(r.UserId, r.TeamId))]);
}

internal sealed class GetCaseRestrictionsHandler(ILitigationDbContext db) : IQueryHandler<GetCaseRestrictionsQuery, CaseRestrictionsResponse>
{
    public async Task<Result<CaseRestrictionsResponse>> Handle(GetCaseRestrictionsQuery query, CancellationToken cancellationToken)
    {
        var legalCase = await db.Cases.AsNoTracking().Include(c => c.Restrictions).SingleOrDefaultAsync(c => c.Id == query.CaseId, cancellationToken);
        return legalCase is null ? LitigationErrors.CaseNotFound : legalCase.ToResponse();
    }
}

/// <summary>
/// Troca as listas do caso sigiloso (muralha ética, seção 6.3). Os usuários precisam ser membros do escritório e as
/// equipes, do escritório. Listas vazias tiram o sigilo; a mudança fica na auditoria.
/// </summary>
public sealed record SetCaseRestrictionsCommand(Guid CaseId, IReadOnlyList<RestrictionEntry>? Allowed, IReadOnlyList<RestrictionEntry>? Blocked)
    : ICommand<CaseRestrictionsResponse>;

internal sealed class SetCaseRestrictionsValidator : AbstractValidator<SetCaseRestrictionsCommand>
{
    public const int MaxEntries = 200;

    public SetCaseRestrictionsValidator()
    {
        RuleFor(c => c.Allowed).Must(l => l is null || l.Count <= MaxEntries).WithErrorCode("litigation.restriction_too_many");
        RuleFor(c => c.Blocked).Must(l => l is null || l.Count <= MaxEntries).WithErrorCode("litigation.restriction_too_many");
    }
}

internal sealed class SetCaseRestrictionsHandler(
    ILitigationDbContext db,
    IMembershipLookup memberships,
    IOrganizationDirectory organization,
    ITenantContext tenant) : ICommandHandler<SetCaseRestrictionsCommand, CaseRestrictionsResponse>
{
    public async Task<Result<CaseRestrictionsResponse>> Handle(SetCaseRestrictionsCommand command, CancellationToken cancellationToken)
    {
        var legalCase = await db.Cases.Include(c => c.Restrictions).SingleOrDefaultAsync(c => c.Id == command.CaseId, cancellationToken);
        if (legalCase is null)
        {
            return LitigationErrors.CaseNotFound;
        }

        var entries = (command.Allowed ?? []).Select(e => new RestrictionInput(RestrictionMode.Allow, e.UserId, e.TeamId))
            .Concat((command.Blocked ?? []).Select(e => new RestrictionInput(RestrictionMode.Block, e.UserId, e.TeamId)))
            .ToList();

        foreach (var userId in entries.Where(e => e.UserId is not null).Select(e => e.UserId!.Value).Distinct())
        {
            if (!(await memberships.GetAsync(userId, cancellationToken)).OrganizationIds.Contains(tenant.RequiredOrganizationId))
            {
                return LitigationErrors.ResponsibleNotMember;
            }
        }

        var teams = entries.Where(e => e.TeamId is not null).Select(e => e.TeamId!.Value).Distinct().ToList();
        if (teams.Count > 0 && (await organization.GetAsync(OrgUnitKind.Team, teams, cancellationToken)).Count != teams.Count)
        {
            return LitigationErrors.TeamNotFound;
        }

        var set = legalCase.SetRestrictions(entries);
        if (set.IsFailure)
        {
            return set.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return legalCase.ToResponse();
    }
}

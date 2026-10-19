using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Access;

/// <summary>Permissões do módulo (nomes do <c>PermissionCatalog</c>), usadas também nos autorizadores de recurso.</summary>
public static class CasePermissions
{
    public const string Read = "cases.read";
    public const string Create = "cases.create";
    public const string Edit = "cases.edit";
    public const string Delete = "cases.delete";
    public const string ManageRestrictions = "cases.restrictions.manage";
    public const string AssessRisk = "cases.risk.assess";
    public const string ApproveSettlements = "cases.settlements.approve";
}

/// <summary>
/// O que o usuário alcança com uma permissão: o escopo das atribuições de perfil (escritório, próprio, equipes,
/// áreas, filiais), as equipes de que ele é membro (para as listas de sigilo) e se gerencia restrições.
/// </summary>
internal sealed record CaseAccessContext(
    Guid UserId,
    bool HasPermission,
    bool Everything,
    bool Own,
    IReadOnlyList<Guid> ScopeTeams,
    IReadOnlyList<Guid> TeamUsers,
    IReadOnlyList<Guid> Areas,
    IReadOnlyList<Guid> Branches,
    IReadOnlyList<Guid> MyTeams,
    bool ManagesRestrictions);

/// <summary>
/// Escopo de dados dos processos (seção 7.2) e casos sigilosos (seção 6.3; decisão de 2026-10-18):
/// <list type="bullet">
/// <item>bloqueado (o usuário ou uma equipe dele) nunca vê o caso;</item>
/// <item>com permitidos, só eles veem (a lista basta, sem o escopo), além de quem gerencia restrições;</item>
/// <item>sem lista, vale o escopo: escritório, próprio (responsável), equipe (a equipe é responsável, ou um membro
/// dela é), área ou filial do processo.</item>
/// </list>
/// Negar um caso sigiloso grava <c>AccessDenied</c> na auditoria; para quem pede, o caso não existe (404).
/// </summary>
internal sealed class CaseAccess(
    ILitigationDbContext db,
    ICurrentAccess access,
    ICurrentUser user,
    IOrganizationDirectory organization,
    IAccessDenials denials,
    IOrganizationAccessDirectory directory)
{
    public async Task<CaseAccessContext> ContextForUserAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        var snapshot = await directory.GetAsync(userId, cancellationToken);
        var grants = snapshot?.Permissions.GetValueOrDefault(permission) ?? [];
        List<Guid> Refs(OfficeAccessScope scope) => [.. grants.Where(g => g.Scope == scope && g.ReferenceId is not null).Select(g => g.ReferenceId!.Value).Distinct()];
        var teams = Refs(OfficeAccessScope.Team);
        return new CaseAccessContext(userId, grants.Count > 0,
            grants.Any(g => g.Scope == OfficeAccessScope.Organization), grants.Any(g => g.Scope == OfficeAccessScope.Own),
            teams, await organization.UsersOfTeamsAsync(teams, cancellationToken),
            Refs(OfficeAccessScope.PracticeArea), Refs(OfficeAccessScope.Branch),
            await organization.TeamsOfUserAsync(userId, cancellationToken),
            snapshot?.Permissions.ContainsKey(CasePermissions.ManageRestrictions) == true);
    }

    public async Task<CaseAccessContext> ContextAsync(string permission, CancellationToken cancellationToken)
    {
        var userId = user.UserId ?? Guid.Empty;
        var grants = await access.ScopesForAsync(permission, cancellationToken);
        var manages = (await access.ScopesForAsync(CasePermissions.ManageRestrictions, cancellationToken)).Count > 0;
        List<Guid> Refs(AccessScope scope) => [.. grants.Where(g => g.Scope == scope && g.ReferenceId is not null).Select(g => g.ReferenceId!.Value).Distinct()];

        var scopeTeams = Refs(AccessScope.Team);
        return new CaseAccessContext(
            userId,
            grants.Count > 0,
            grants.Any(g => g.Scope == AccessScope.Organization),
            grants.Any(g => g.Scope == AccessScope.Own),
            scopeTeams,
            await organization.UsersOfTeamsAsync(scopeTeams, cancellationToken),
            Refs(AccessScope.PracticeArea),
            Refs(AccessScope.Branch),
            await organization.TeamsOfUserAsync(userId, cancellationToken),
            manages);
    }

    /// <summary>Os casos que o contexto alcança (um único filtro, traduzido em SQL com <c>EXISTS</c>).</summary>
    public static IQueryable<LegalCase> Visible(IQueryable<LegalCase> cases, CaseAccessContext context)
    {
        if (!context.HasPermission)
        {
            return cases.Where(_ => false);
        }

        var (me, myTeams, everything, own, manages) = (context.UserId, context.MyTeams, context.Everything, context.Own, context.ManagesRestrictions);
        var (scopeTeams, teamUsers, areas, branches) = (context.ScopeTeams, context.TeamUsers, context.Areas, context.Branches);
        return cases.Where(c =>
            !c.Restrictions.Any(r => r.Mode == RestrictionMode.Block && (r.UserId == me || (r.TeamId != null && myTeams.Contains(r.TeamId.Value))))
            && (c.Restrictions.Any(r => r.Mode == RestrictionMode.Allow)
                ? manages || c.Restrictions.Any(r => r.Mode == RestrictionMode.Allow && (r.UserId == me || (r.TeamId != null && myTeams.Contains(r.TeamId.Value))))
                : everything
                    || (own && c.Assignments.Any(a => a.UserId == me))
                    || c.Assignments.Any(a => (a.TeamId != null && scopeTeams.Contains(a.TeamId.Value)) || (a.UserId != null && teamUsers.Contains(a.UserId.Value)))
                    || (c.PracticeAreaId != null && areas.Contains(c.PracticeAreaId.Value))
                    || (c.BranchId != null && branches.Contains(c.BranchId.Value))));
    }

    /// <summary>O caso está ao alcance da permissão? Fora dele, 404 (e, se for sigiloso, a negativa vai para a auditoria).</summary>
    public async Task<Result> AuthorizeAsync(Guid caseId, string permission, CancellationToken cancellationToken)
    {
        var context = await ContextAsync(permission, cancellationToken);
        if (await Visible(db.Cases.Where(c => c.Id == caseId), context).AnyAsync(cancellationToken))
        {
            return Result.Success;
        }

        if (await db.Cases.AnyAsync(c => c.Id == caseId && c.Restrictions.Any(), cancellationToken))
        {
            await denials.RecordAsync(nameof(LegalCase), caseId, cancellationToken);
        }

        return LitigationErrors.CaseNotFound;
    }
}

/// <summary>Grava na trilha de auditoria a negativa de acesso a um caso sigiloso (seção 6.3).</summary>
public interface IAccessDenials
{
    Task RecordAsync(string entityType, Guid entityId, CancellationToken cancellationToken);
}

/// <summary>
/// Os casos sigilosos que o usuário não vê com <c>cases.read</c>, para os outros módulos esconderem os dados ligados a
/// eles (documentos, prazos, horas, despesas).
/// </summary>
internal sealed class CaseVisibility(ILitigationDbContext db, CaseAccess access) : ICaseVisibility
{
    public async Task<IReadOnlyCollection<Guid>> HiddenCaseIdsAsync(CancellationToken cancellationToken)
    {
        var context = await access.ContextAsync(CasePermissions.Read, cancellationToken);
        var restricted = db.Cases.AsNoTracking().Where(c => c.Restrictions.Any());
        var visible = CaseAccess.Visible(restricted, context).Select(c => c.Id);
        return await restricted.Where(c => !visible.Contains(c.Id)).Select(c => c.Id).ToListAsync(cancellationToken);
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Organization.Domain.Roles;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Organization.Application.Access;

/// <summary>
/// Permissões de um membro numa organização, com o escopo de cada uma (a mesma permissão pode vir de mais de um
/// perfil, com escopos diferentes). É o que fica em cache por 5 minutos.
/// </summary>
public sealed record AccessSnapshot(Guid MemberId, bool MfaRequired, IReadOnlyDictionary<string, IReadOnlyList<ScopeGrant>> Permissions);

/// <summary>
/// Lê o acesso do usuário na organização <b>ativa</b>: o filtro de tenant só deixa ver os dados dela, então pedir o
/// acesso em outra organização devolve nulo. Usa o cache e, sem ele, o banco.
/// </summary>
internal sealed class AccessReader(IOrganizationDbContext db, ITenantContext tenant, IAccessCache cache)
{
    public Task<AccessSnapshot?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        tenant.OrganizationId == organizationId
            ? cache.GetOrLoadAsync(organizationId, userId, () => LoadAsync(organizationId, userId, cancellationToken), cancellationToken)
            : Task.FromResult<AccessSnapshot?>(null);

    private async Task<AccessSnapshot?> LoadAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        var memberId = await db.Members.AsNoTracking().Where(m => m.UserId == userId).Select(m => (Guid?)m.Id).SingleOrDefaultAsync(cancellationToken);
        if (memberId is null)
        {
            return null;
        }

        var mfaRequired = await db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.MfaRequired).SingleAsync(cancellationToken);
        var grants = await (
                from assignment in db.RoleAssignments.AsNoTracking()
                where assignment.MemberId == memberId
                join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                from permission in role.Permissions
                select new { permission.Permission, assignment.Scope, assignment.ScopeRefId })
            .ToListAsync(cancellationToken);

        var permissions = grants
            .GroupBy(g => g.Permission, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ScopeGrant>)[.. g.Select(x => new ScopeGrant(ToAccessScope(x.Scope), x.ScopeRefId)).Distinct()],
                StringComparer.Ordinal);
        return new AccessSnapshot(memberId.Value, mfaRequired, permissions);
    }

    internal static AccessScope ToAccessScope(DataScope scope) => scope switch
    {
        DataScope.Own => AccessScope.Own,
        DataScope.Team => AccessScope.Team,
        DataScope.PracticeArea => AccessScope.PracticeArea,
        DataScope.Branch => AccessScope.Branch,
        _ => AccessScope.Organization,
    };
}

/// <summary>
/// Primeira etapa da autorização (seção 7.2). Permissões universais (a própria conta, criar escritório) bastam com
/// login. As demais exigem organização ativa, pertença e o perfil; se a organização exige MFA e o token não traz
/// <c>amr=mfa</c>, a resposta é <see cref="PermissionDecision.MfaRequired"/>.
/// </summary>
internal sealed class PermissionChecker(AccessReader access) : IPermissionChecker
{
    public async Task<PermissionDecision> CheckAsync(PermissionCheck check, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        if (PermissionCatalog.IsUniversal(check.Permission))
        {
            return PermissionDecision.Allowed;
        }

        if (check.OrganizationId is not { } organizationId
            || await access.GetAsync(organizationId, check.UserId, cancellationToken) is not { } snapshot)
        {
            return PermissionDecision.Denied;
        }

        if (snapshot.MfaRequired && !check.MultiFactorAuthenticated)
        {
            return PermissionDecision.MfaRequired;
        }

        return snapshot.Permissions.ContainsKey(check.Permission) ? PermissionDecision.Allowed : PermissionDecision.Denied;
    }
}

/// <summary>Acesso do usuário corrente na organização ativa, para os autorizadores de recurso dos módulos.</summary>
internal sealed class CurrentAccess(AccessReader access, ICurrentUser user, ITenantContext tenant) : ICurrentAccess
{
    public async Task<Guid?> MemberIdAsync(CancellationToken cancellationToken) =>
        (await SnapshotAsync(cancellationToken))?.MemberId;

    public async Task<IReadOnlyList<ScopeGrant>> ScopesForAsync(string permission, CancellationToken cancellationToken) =>
        (await SnapshotAsync(cancellationToken))?.Permissions.GetValueOrDefault(permission) ?? [];

    private Task<AccessSnapshot?> SnapshotAsync(CancellationToken cancellationToken) =>
        user.UserId is { } userId && tenant.OrganizationId is { } organizationId
            ? access.GetAsync(organizationId, userId, cancellationToken)
            : Task.FromResult<AccessSnapshot?>(null);
}

/// <summary>Pertenças do usuário pelo diretório <c>user_organizations</c> (sem consulta entre tenants), com cache.</summary>
internal sealed class MembershipLookup(IOrganizationDbContext db, IAccessCache cache) : IMembershipLookup
{
    public async Task<MembershipSnapshot> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await cache.GetMembershipsAsync(userId, cancellationToken) is { } cached)
        {
            return cached;
        }

        var rows = await db.UserOrganizations.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new { u.OrganizationId, u.LastSelectedAt })
            .ToListAsync(cancellationToken);
        var snapshot = new MembershipSnapshot(
            [.. rows.Select(r => r.OrganizationId)],
            rows.Where(r => r.LastSelectedAt is not null).OrderByDescending(r => r.LastSelectedAt).Select(r => (Guid?)r.OrganizationId).FirstOrDefault());
        await cache.SetMembershipsAsync(userId, snapshot, cancellationToken);
        return snapshot;
    }
}

/// <summary>
/// Membros ativos da organização ativa: os que não saíram nem foram removidos (exclusão lógica). O filtro de tenant e
/// a RLS limitam a contagem ao escritório da requisição.
/// </summary>
internal sealed class OrganizationMembers(IOrganizationDbContext db) : IOrganizationMembers
{
    public Task<int> CountActiveAsync(CancellationToken cancellationToken) =>
        db.Members.AsNoTracking().CountAsync(cancellationToken);
}

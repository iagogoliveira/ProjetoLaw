using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Contracts.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Organization.Application.OrgStructure;

/// <summary>A estrutura da organização ativa (filtro de tenant e RLS), sem os cadastros excluídos.</summary>
internal sealed class OrganizationDirectory(IOrganizationDbContext db) : IOrganizationDirectory
{
    public async Task<IReadOnlyList<OrgUnit>> GetAsync(OrgUnitKind kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = kind switch
        {
            OrgUnitKind.PracticeArea => await db.PracticeAreas.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.Name }).ToListAsync(cancellationToken),
            OrgUnitKind.Branch => await db.Branches.AsNoTracking().Where(b => ids.Contains(b.Id)).Select(b => new { b.Id, b.Name }).ToListAsync(cancellationToken),
            _ => await db.Teams.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken),
        };
        return [.. rows.Select(r => new OrgUnit(r.Id, kind, r.Name))];
    }

    public async Task<IReadOnlyList<OrgUnit>> ListAsync(OrgUnitKind kind, CancellationToken cancellationToken)
    {
        var rows = kind switch
        {
            OrgUnitKind.PracticeArea => await db.PracticeAreas.AsNoTracking().Select(a => new { a.Id, a.Name }).ToListAsync(cancellationToken),
            OrgUnitKind.Branch => await db.Branches.AsNoTracking().Select(b => new { b.Id, b.Name }).ToListAsync(cancellationToken),
            _ => await db.Teams.AsNoTracking().Select(t => new { t.Id, t.Name }).ToListAsync(cancellationToken),
        };
        return [.. rows.Select(r => new OrgUnit(r.Id, kind, r.Name))];
    }

    public async Task<IReadOnlyList<Guid>> MemberUserIdsAsync(CancellationToken cancellationToken) =>
        await db.Members.AsNoTracking().Select(m => m.UserId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> TeamsOfUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.TeamMembers.AsNoTracking()
            .Join(db.Members, t => t.MemberId, m => m.Id, (t, m) => new { t.TeamId, m.UserId })
            .Where(x => x.UserId == userId)
            .Select(x => x.TeamId)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> UsersOfTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken) =>
        teamIds.Count == 0
            ? []
            : await db.TeamMembers.AsNoTracking()
                .Where(t => teamIds.Contains(t.TeamId))
                .Join(db.Members, t => t.MemberId, m => m.Id, (t, m) => m.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
}

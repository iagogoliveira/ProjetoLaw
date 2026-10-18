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
}

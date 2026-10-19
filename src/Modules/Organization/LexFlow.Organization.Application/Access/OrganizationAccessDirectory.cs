using LexFlow.Application.Abstractions;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Contracts.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Organization.Application.Access;

internal sealed class OrganizationAccessDirectory(IOrganizationDbContext db, AccessReader reader, ITenantContext tenant) : IOrganizationAccessDirectory
{
    public async Task<OfficeUserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = tenant.OrganizationId is { } office ? await reader.GetAsync(office, userId, cancellationToken) : null;
        return snapshot is null ? null : new OfficeUserAccess(snapshot.MfaRequired, snapshot.Permissions.ToDictionary(
            p => p.Key, p => (IReadOnlyList<OfficeScope>)p.Value.Select(s => new OfficeScope(Enum.Parse<OfficeAccessScope>(s.Scope.ToString()), s.ReferenceId)).ToList(),
            StringComparer.Ordinal));
    }
    public async Task<IReadOnlyList<Guid>> UserIdsAsync(CancellationToken cancellationToken) =>
        await db.Members.AsNoTracking().Select(m => m.UserId).ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<Guid>> OfficeIdsAsync(CancellationToken cancellationToken) =>
        await db.Organizations.AsNoTracking().Select(o => o.Id).ToListAsync(cancellationToken);
}

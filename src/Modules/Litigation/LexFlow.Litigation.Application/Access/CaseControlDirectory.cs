using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Organization.Contracts.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Access;

internal sealed class CaseControlDirectory(
    ILitigationDbContext db,
    CaseAccess access,
    IOrganizationAccessDirectory organizationAccess) : ICaseControlDirectory
{
    public async Task<IReadOnlyList<ActiveControlCase>> ActiveAsync(
        Guid userId,
        bool multiFactorAuthenticated,
        CancellationToken cancellationToken)
    {
        var member = await organizationAccess.GetAsync(userId, cancellationToken);
        if (member is null || member.MfaRequired && !multiFactorAuthenticated)
        {
            return [];
        }

        var caseRead = await access.ContextForUserAsync(userId, "cases.read", cancellationToken);
        var controlRead = await access.ContextForUserAsync(userId, "schedule.control.read", cancellationToken);
        var cases = CaseAccess.Visible(db.Cases.AsNoTracking(), caseRead);
        cases = CaseAccess.Visible(cases, controlRead);

        return await cases
            .Where(c => c.Status == CaseStatus.Active)
            .OrderBy(c => c.InternalFolder)
            .ThenBy(c => c.Id)
            .Select(c => new ActiveControlCase(c.Id, c.InternalFolder, c.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

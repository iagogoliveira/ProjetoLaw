using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain.Hearings;
using LexFlow.Organization.Contracts.OrgStructure;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Hearings;

internal sealed class HearingDigest(ILitigationDbContext db, ICaseDirectory cases, IOrganizationAccessDirectory members) : IHearingDigest
{
    public async Task<IReadOnlyList<HearingDigestItem>> ReadAsync(Guid userId, DateTimeOffset from,
        DateTimeOffset until, CancellationToken cancellationToken)
    {
        if (until <= from || until - from > TimeSpan.FromDays(2) || await members.GetAsync(userId, cancellationToken) is null) { return []; }
        var scope = await cases.ScopeForUserAsync(userId, "cases.read", cancellationToken);
        return await db.Hearings.AsNoTracking().Where(h => scope.VisibleIds.Contains(h.LegalCaseId)
                && h.LawyerUserId == userId && h.Status == HearingStatus.Scheduled && h.ScheduledAt >= from && h.ScheduledAt < until)
            .OrderBy(h => h.ScheduledAt).ThenBy(h => h.Id)
            .Select(h => new HearingDigestItem(h.Id, h.LegalCaseId, h.ScheduledAt)).ToListAsync(cancellationToken);
    }
}

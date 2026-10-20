using LexFlow.Litigation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

internal sealed class SchedulingTimeline(ISchedulingDbContext db, ICaseDirectory cases) : ITimelineContributor
{
    public async Task<IReadOnlyList<TimelineEntry>> GetAsync(Guid caseId, TimelinePosition? before, int limit, CancellationToken cancellationToken)
    {
        if (!(await cases.VisibleIdsAsync("schedule.deadlines.read", cancellationToken)).Contains(caseId)) { return []; }
        var query = db.DeadlineActivities.AsNoTracking().Where(a => a.LegalCaseId == caseId);
        if (before is not null)
        {
            var positionId = before.Id.StartsWith("deadline:", StringComparison.Ordinal) && Guid.TryParseExact(before.Id.AsSpan(9), "N", out var id)
                ? id : string.CompareOrdinal("deadline:", before.Id) < 0 ? Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff") : Guid.Empty;
            query = query.Where(a => a.OccurredAt < before.At || (a.OccurredAt == before.At && a.Id.CompareTo(positionId) < 0));
        }
        var rows = await query.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(limit + 1).ToListAsync(cancellationToken);
        return rows.Select(a => new TimelineEntry($"deadline:{a.Id:N}", a.OccurredAt, a.Kind,
                new Dictionary<string, string?>(StringComparer.Ordinal) { ["actorUserId"] = a.ActorUserId.ToString(), ["calendarEntryId"] = a.CalendarEntryId?.ToString() }, a.DeadlineId))
            .Where(entry => before is null || before.Precedes(entry)).Take(limit).ToList();
    }
}

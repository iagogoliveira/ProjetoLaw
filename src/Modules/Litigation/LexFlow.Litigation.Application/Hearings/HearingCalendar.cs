using System.Text.Json;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain.Hearings;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Hearings;

public sealed class HearingCalendar(ILitigationDbContext db, ICaseDirectory cases) : IHearingCalendar
{
    public async Task<IReadOnlyList<HearingCalendarItem>> ListAsync(HearingCalendarWindow window, CancellationToken cancellationToken)
    {
        if (window.Limit is < 1 or > 101 || window.Until <= window.From || window.Until - window.From > TimeSpan.FromDays(367)) { return []; }
        var visible = await cases.VisibleIdsAsync("cases.read", cancellationToken);
        var source = db.Hearings.AsNoTracking().Where(h => visible.Contains(h.LegalCaseId)
            && h.ScheduledAt >= window.From && h.ScheduledAt < window.Until);
        if (!window.IncludeClosed) { source = source.Where(h => h.Status == HearingStatus.Scheduled); }
        if (window.Users is { } users) { source = source.Where(h => h.LawyerUserId != null && users.Contains(h.LawyerUserId.Value)); }
        if (window.AfterAt is { } after)
        {
            source = source.Where(h => h.ScheduledAt > after || (h.ScheduledAt == after && window.IncludeEqual
                && (window.AfterId == null || h.Id.CompareTo(window.AfterId.Value) > 0)));
        }
        var items = await source.OrderBy(h => h.ScheduledAt).ThenBy(h => h.Id).Take(window.Limit).ToListAsync(cancellationToken);
        return items.Select(h => new HearingCalendarItem(h.Id, h.LegalCaseId, h.ScheduledAt, h.LawyerUserId,
            JsonNamingPolicy.CamelCase.ConvertName(h.Type.ToString()), JsonNamingPolicy.CamelCase.ConvertName(h.Status.ToString()))).ToArray();
    }
}

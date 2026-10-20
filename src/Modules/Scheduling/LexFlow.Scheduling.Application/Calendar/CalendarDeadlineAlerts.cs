using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Contracts;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.Calendar;

/// <summary>Compara o calendário efetivo antes/depois; preserva datas e memória de prazos confirmados.</summary>
internal sealed class CalendarDeadlineAlerts(ISchedulingDbContext db, ICaseDirectory cases, ICourtDirectory courts,
    IClock clock, ICurrentUser user, IIntegrationEventPublisher events)
{
    public async Task ChangedAsync(CalendarEntry? before, CalendarEntry? after, CancellationToken ct)
    {
        var today = clock.Today;
        var deadlines = await db.Deadlines.Where(d => d.FatalDueOn >= today
            && d.Status != WorkItemStatus.Completed && d.Status != WorkItemStatus.Missed).OrderBy(d => d.Id).ToListAsync(ct);
        foreach (var deadline in deadlines)
        {
            if (!await AffectedAsync(deadline, before, after, ct)) { continue; }
            var now = clock.UtcNow;
            deadline.FlagCalendarChange(now);
            var activity = DeadlineActivity.Record(deadline, "scheduling.calendar_changed", user.UserId ?? Guid.Empty, now, (after ?? before)!.Id);
            db.DeadlineActivities.Add(activity);
            events.Publish(new DeadlineAlertIntegrationEvent(activity.Id, deadline.Id, deadline.LegalCaseId, activity.Kind));
        }
    }

    private async Task<bool> AffectedAsync(Deadline d, CalendarEntry? before, CalendarEntry? after, CancellationToken ct)
    {
        // Prazos novos preservam o local confirmado; os legados usam o local atual do processo.
        var location = d.Rule is null ? await cases.LocationAsync(d.LegalCaseId, ct) : null;
        var courtId = d.CourtId ?? location?.CourtId;
        var court = courtId is { } id ? await courts.GetAsync(id, ct) : null;
        var state = d.Rule is null ? CalculateDeadlineHandler.DefaultState(court) : d.StateCode;
        var municipality = d.Rule is null ? location?.District : d.Municipality;
        var from = d.Rule?.TriggerDate ?? d.StartOn;
        var oldDays = Days(before);
        var newDays = Days(after);
        return !oldDays.SetEquals(newDays);

        HashSet<(DateOnly, bool)> Days(CalendarEntry? entry) => entry is null || !entry.AppliesTo(state, municipality, courtId)
            ? [] : entry.DaysBetween(from, d.FatalDueOn).Select(day => (day.Date, day.IsSuspension)).ToHashSet();
    }

    public static CalendarEntry Copy(CalendarEntry entry) => CalendarEntry.Create(new CalendarEntryInput(entry.Name, entry.Kind,
        entry.Scope, entry.StateCode, entry.Municipality, entry.CourtId, entry.StartsOn, entry.EndsOn, entry.RecursYearly, entry.Note)).Value;
}

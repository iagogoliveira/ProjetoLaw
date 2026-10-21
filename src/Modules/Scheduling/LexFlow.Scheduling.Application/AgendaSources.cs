using System.Text.Json;
using LexFlow.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

internal sealed class AgendaSources(ISchedulingDbContext db, AgendaAccess access)
{
    public async Task<IReadOnlyList<AgendaItem>> DeadlinesAsync(ListAgendaQuery query, AgendaCursor? after, IReadOnlyCollection<Guid>? users, CancellationToken ct)
    {
        var scope = await access.ScopeAsync("schedule.deadlines.read", ct);
        var source = db.Deadlines.AsNoTracking().Where(d => scope.Cases.Contains(d.LegalCaseId) && d.FatalDueOn >= query.From && d.FatalDueOn < query.Until);
        if (users is not null) { source = source.Where(d => users.Contains(d.AssigneeUserId)); }
        if (!query.IncludeClosed) { source = source.Where(d => d.Status == WorkItemStatus.Pending || d.Status == WorkItemStatus.InProgress); }
        if (after is not null)
        {
            var day = AgendaTime.Day(after.At);
            var equal = after.At == AgendaTime.Start(day) && AgendaKind.Deadline >= after.Kind;
            source = source.Where(d => d.FatalDueOn > day || (d.FatalDueOn == day && equal && (after.Kind != AgendaKind.Deadline || d.Id.CompareTo(after.Id) > 0)));
        }
        var items = await source.OrderBy(d => d.FatalDueOn).ThenBy(d => d.Id).Take(query.Limit + 1).ToListAsync(ct);
        return items.Select(d => new AgendaItem(AgendaKind.Deadline, d.Id, d.LegalCaseId, d.Title, AgendaTime.Start(d.FatalDueOn), null,
            true, d.AssigneeUserId, JsonNamingPolicy.CamelCase.ConvertName(d.Status.ToString()), d.InternalDueOn)).ToArray();
    }

    public async Task<IReadOnlyList<AgendaItem>> TasksAsync(ListAgendaQuery query, AgendaCursor? after, IReadOnlyCollection<Guid>? users, CancellationToken ct)
    {
        var scope = await access.ScopeAsync("schedule.tasks.read", ct);
        var source = db.Tasks.AsNoTracking().Where(t => t.DueOn >= query.From && t.DueOn < query.Until
            && (t.LegalCaseId != null ? scope.Cases.Contains(t.LegalCaseId.Value) : scope.AllAdministrative || scope.Users.Contains(t.AssigneeUserId)));
        if (users is not null) { source = source.Where(t => users.Contains(t.AssigneeUserId)); }
        if (!query.IncludeClosed) { source = source.Where(t => t.Status != TaskState.Completed && t.Status != TaskState.Cancelled); }
        if (after is not null)
        {
            var day = AgendaTime.Day(after.At);
            var equal = after.At == AgendaTime.Start(day) && AgendaKind.Task >= after.Kind;
            source = source.Where(t => t.DueOn > day || (t.DueOn == day && equal && (after.Kind != AgendaKind.Task || t.Id.CompareTo(after.Id) > 0)));
        }
        var items = await source.OrderBy(t => t.DueOn).ThenBy(t => t.Id).Take(query.Limit + 1).ToListAsync(ct);
        return items.Select(t => new AgendaItem(AgendaKind.Task, t.Id, t.LegalCaseId, t.Title, AgendaTime.Start(t.DueOn), null,
            true, t.AssigneeUserId, JsonNamingPolicy.CamelCase.ConvertName(t.Status.ToString()), null)).ToArray();
    }

    public async Task<IReadOnlyList<AgendaItem>> EventsAsync(ListAgendaQuery query, AgendaCursor? after, IReadOnlyCollection<Guid>? users, CancellationToken ct)
    {
        var scope = await access.ScopeAsync("schedule.events.read", ct);
        var from = AgendaTime.Start(query.From);
        var until = AgendaTime.Start(query.Until);
        var source = db.Events.AsNoTracking().Where(e => e.StartsAt < until && e.EndsAt > from
            && (e.LegalCaseId != null ? scope.Cases.Contains(e.LegalCaseId.Value) : scope.AllAdministrative || scope.Users.Contains(e.AssigneeUserId)));
        if (users is not null) { source = source.Where(e => users.Contains(e.AssigneeUserId)); }
        if (!query.IncludeClosed) { source = source.Where(e => e.Status == CalendarEventState.Scheduled); }
        if (after is not null)
        {
            var equal = AgendaKind.Event >= after.Kind;
            source = source.Where(e => e.StartsAt > after.At || (e.StartsAt == after.At && equal && (after.Kind != AgendaKind.Event || e.Id.CompareTo(after.Id) > 0)));
        }
        var items = await source.OrderBy(e => e.StartsAt).ThenBy(e => e.Id).Take(query.Limit + 1).ToListAsync(ct);
        return items.Select(e => new AgendaItem(AgendaKind.Event, e.Id, e.LegalCaseId, e.Title, e.StartsAt, e.EndsAt,
            false, e.AssigneeUserId, JsonNamingPolicy.CamelCase.ConvertName(e.Status.ToString()), null)).ToArray();
    }
}

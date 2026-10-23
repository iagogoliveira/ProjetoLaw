using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.CalendarSync;

internal sealed partial class CalendarSyncEngine
{
    private async Task<Result> ExportNewAsync(CalendarConnection connection, CancellationToken ct)
    {
        var eventsScope = await access.ScopeAsync(connection.UserId, "schedule.events.read", ct);
        var deadlinesScope = await access.ScopeAsync(connection.UserId, "schedule.deadlines.read", ct);
        var from = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(connection.From.ToDateTime(TimeOnly.MinValue), clock.LegalTimeZone));
        var until = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(connection.Until.ToDateTime(TimeOnly.MinValue), clock.LegalTimeZone));
        var eventIds = await db.CalendarSyncLinks.Where(l => l.ConnectionId == connection.Id && l.Kind == CalendarItemKind.Event).Select(l => l.LocalId).ToArrayAsync(ct);
        var deadlineIds = await db.CalendarSyncLinks.Where(l => l.ConnectionId == connection.Id && l.Kind == CalendarItemKind.Deadline).Select(l => l.LocalId).ToArrayAsync(ct);
        var events = await db.Events.Where(e => e.AssigneeUserId == connection.UserId && e.StartsAt < until && e.EndsAt > from
            && !eventIds.Contains(e.Id) && (e.LegalCaseId != null ? eventsScope.Cases.Contains(e.LegalCaseId.Value) : eventsScope.AllAdministrative))
            .OrderBy(e => e.Id).Take(MaximumItems + 1).ToListAsync(ct);
        var deadlines = await db.Deadlines.Where(d => d.AssigneeUserId == connection.UserId && d.FatalDueOn >= connection.From && d.FatalDueOn < connection.Until
            && !deadlineIds.Contains(d.Id) && deadlinesScope.Cases.Contains(d.LegalCaseId)).OrderBy(d => d.Id).Take(MaximumItems + 1).ToListAsync(ct);
        if (events.Count + deadlines.Count > MaximumItems) { return CalendarSyncErrors.SyncLimit; }
        foreach (var item in events.Select(Snapshot).Concat(deadlines.Select(Snapshot)))
        {
            var outcome = await PushNewAsync(connection, item, ct);
            if (outcome.IsFailure) { return outcome; }
        }
        return Result.Success;
    }

    private CalendarSyncWrite Write(LocalCalendarSnapshot local)
    {
        var path = local.Kind == CalendarItemKind.Event ? $"agenda/compromissos/{local.Id:D}" : $"prazos/{local.Id:D}";
        var root = new Uri(settings.PublicWebUrl.TrimEnd('/') + "/", UriKind.Absolute);
        return new CalendarSyncWrite($"{tenant.RequiredOrganizationId:N}:{local.Kind}:{local.Id:N}", local.Kind,
            local.StartsAt, local.EndsAt, local.Day, local.IsCancelled, new Uri(root, path).AbsoluteUri);
    }

    private async Task<Result> PushNewAsync(CalendarConnection connection, LocalCalendarSnapshot local, CancellationToken ct)
    {
        if (!await AllowedAsync(connection, local, ct)) { return Result.Success; }
        var result = await provider.WriteAsync(Context(connection), Write(local), null, null, ct);
        if (result.IsFailure) { return result.Error; }
        if (!CalendarSyncSnapshots.Valid(result.Value)) { return CalendarSyncErrors.InvalidRemote; }
        var link = CalendarSyncLink.Create(connection.Id, local.Kind, local.Id, local.CaseId, result.Value.Id);
        link.Remember(result.Value.Version, CalendarSyncSnapshots.Json(result.Value), local.Fingerprint);
        if (!Equivalent(local, result.Value) && local.Kind == CalendarItemKind.Event)
        { Pending(link, local, result.Value, CalendarPendingReason.Conflict); }
        db.CalendarSyncLinks.Add(link);
        if (!Equivalent(local, result.Value) && local.Kind == CalendarItemKind.Deadline)
        { return await PushAsync(connection, link, local, result.Value, ct); }
        return Result.Success;
    }

    private async Task<Result> PushAsync(CalendarConnection connection, CalendarSyncLink link, LocalCalendarSnapshot local,
        RemoteCalendarEvent remote, CancellationToken ct)
    {
        if (!await AllowedAsync(connection, local, ct)) { return Result.Success; }
        var missing = string.Equals(remote.Version, "missing", StringComparison.Ordinal);
        var result = await provider.WriteAsync(Context(connection), Write(local), missing ? null : remote.Id, missing ? null : remote.Version, ct);
        if (result.IsFailure)
        {
            if (!string.Equals(result.Error.Code, CalendarSyncErrors.RemoteConflict.Code, StringComparison.Ordinal)) { return result.Error; }
            if (local.Kind == CalendarItemKind.Deadline) { return result.Error; }
            var current = await provider.GetAsync(Context(connection), link.RemoteId, ct);
            if (current.IsFailure) { return current.Error; }
            return Pending(link, local, current.Value ?? remote with { Version = "missing", IsCancelled = true }, CalendarPendingReason.Conflict);
        }
        return !CalendarSyncSnapshots.Valid(result.Value) ? CalendarSyncErrors.InvalidRemote : Remember(link, local, result.Value);
    }

    private async Task<bool> AllowedAsync(CalendarConnection connection, LocalCalendarSnapshot local, CancellationToken ct) =>
        await access.CanConnectAsync(connection.UserId, ct) && await access.CanUseAsync(connection.UserId, local.CaseId,
            local.Kind == CalendarItemKind.Event ? "schedule.events.read" : "schedule.deadlines.read", ct);
}

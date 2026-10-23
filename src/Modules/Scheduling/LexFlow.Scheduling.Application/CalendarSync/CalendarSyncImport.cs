using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.CalendarSync;

internal sealed partial class CalendarSyncEngine
{
    private async Task<Result> ImportAsync(CalendarConnection connection, RemoteCalendarEvent remote, CancellationToken ct)
    {
        if (remote.IsCancelled || remote.ClientKey is not null || !await access.CanEditEventAsync(connection.UserId, null, ct)) { return Result.Success; }
        var input = CalendarSyncSnapshots.Input(remote, connection.UserId, null, null);
        var entry = input.IsSuccess ? CalendarEvent.Create(input.Value) : Result.Failure<CalendarEvent>(input.Error);
        var link = CalendarSyncLink.Create(connection.Id, CalendarItemKind.Event, entry.IsSuccess ? entry.Value.Id : null, null, remote.Id);
        if (entry.IsSuccess)
        {
            db.Events.Add(entry.Value);
            link.Remember(remote.Version, CalendarSyncSnapshots.Json(remote), CalendarSyncSnapshots.Fingerprint(entry.Value));
        }
        else { link.Pend(CalendarPendingReason.Unsupported, CalendarSyncSnapshots.Json(remote), string.Empty); }
        db.CalendarSyncLinks.Add(link);
        return Result.Success;
    }

    private async Task<Result> SynchronizeLinkAsync(CalendarConnection connection, CalendarSyncLink link, CancellationToken ct)
    {
        var local = await LocalAsync(connection, link, ct);
        if (local is null && link.LocalId is not null) { return Result.Success; }
        var found = await provider.GetAsync(Context(connection), link.RemoteId, ct);
        if (found.IsFailure) { return found.Error; }
        var baseline = CalendarSyncSnapshots.Read(link.RemoteSnapshot);
        var previous = baseline ?? (link.PendingSnapshot is null ? null : CalendarSyncSnapshots.Read(link.PendingSnapshot));
        var remote = found.Value ?? (previous is null ? null : previous with { Version = "missing", IsCancelled = true });
        if (remote is null) { return Result.Success; }
        if (!CalendarSyncSnapshots.Valid(remote)) { return CalendarSyncErrors.InvalidRemote; }
        if (local is not null && !Overlaps(connection, local.StartsAt, local.EndsAt, local.Day)
            && !Overlaps(connection, remote.StartsAt, remote.EndsAt, remote.Day)) { return Result.Success; }
        if (local is null)
        {
            if (link.PendingReason is not null) { link.Pend(CalendarPendingReason.Unsupported, CalendarSyncSnapshots.Json(remote), string.Empty); }
            return Result.Success;
        }
        if (link.PendingReason is { } reason)
        {
            link.Pend(remote.IsCancelled && !local.IsCancelled ? CalendarPendingReason.ExternalDeletion
                : reason == CalendarPendingReason.Unsupported ? reason : CalendarPendingReason.Conflict,
                CalendarSyncSnapshots.Json(remote), local.Fingerprint);
            return Result.Success;
        }
        return await ReconcileAsync(connection, link, local, remote, baseline, ct);
    }

    private async Task<Result> ReconcileAsync(CalendarConnection connection, CalendarSyncLink link, LocalCalendarSnapshot local,
        RemoteCalendarEvent remote, RemoteCalendarEvent? baseline, CancellationToken ct)
    {
        if (link.Kind == CalendarItemKind.Deadline)
        {
            return Equivalent(local, remote) ? Remember(link, local, remote) : await PushAsync(connection, link, local, remote, ct);
        }
        var localChanged = !string.Equals(local.Fingerprint, link.LocalFingerprint, StringComparison.Ordinal);
        var remoteChanged = baseline is null || remote with { Version = baseline.Version } != baseline;
        if (remote.IsCancelled && !local.IsCancelled) { return Pending(link, local, remote, CalendarPendingReason.ExternalDeletion); }
        if (local.IsCancelled)
        {
            if (localChanged && remoteChanged && !Converged(local, remote, baseline))
            { return Pending(link, local, remote, CalendarPendingReason.Conflict); }
            if (localChanged) { return await PushAsync(connection, link, local, remote, ct); }
            return remoteChanged && !remote.IsCancelled ? Pending(link, local, remote, CalendarPendingReason.Unsupported) : Remember(link, local, remote);
        }
        if (remoteChanged)
        {
            if (localChanged && !Converged(local, remote, baseline)) { return Pending(link, local, remote, CalendarPendingReason.Conflict); }
            if (localChanged) { return Remember(link, local, remote); }
            var applied = await ApplyAsync(connection, link, remote, baseline, ct);
            return applied.IsFailure ? Pending(link, local, remote, CalendarPendingReason.Unsupported) : Result.Success;
        }
        return localChanged ? await PushAsync(connection, link, local, remote, ct) : Remember(link, local, remote);
    }

    private async Task<Result> ApplyAsync(CalendarConnection connection, CalendarSyncLink link, RemoteCalendarEvent remote,
        RemoteCalendarEvent? baseline, CancellationToken ct)
    {
        var entry = await db.Events.SingleOrDefaultAsync(e => e.Id == link.LocalId && e.AssigneeUserId == connection.UserId, ct);
        if (entry is null || !await access.CanEditEventAsync(connection.UserId, entry.LegalCaseId, ct)) { return CalendarSyncErrors.AccessUnavailable; }
        Result outcome;
        if (remote.IsCancelled) { outcome = entry.Status == CalendarEventState.Cancelled ? Result.Success : entry.Cancel(); }
        else
        {
            var input = CalendarSyncSnapshots.Input(remote, connection.UserId, entry, baseline);
            outcome = input.IsFailure ? input.Error : entry.Edit(input.Value);
        }
        if (outcome.IsFailure) { return outcome; }
        link.Remember(remote.Version, CalendarSyncSnapshots.Json(remote), CalendarSyncSnapshots.Fingerprint(entry), remote.Id);
        return Result.Success;
    }

    private static Result Remember(CalendarSyncLink link, LocalCalendarSnapshot local, RemoteCalendarEvent remote)
    { link.Remember(remote.Version, CalendarSyncSnapshots.Json(remote), local.Fingerprint, remote.Id); return Result.Success; }

    private static Result Pending(CalendarSyncLink link, LocalCalendarSnapshot local, RemoteCalendarEvent remote, CalendarPendingReason reason)
    { link.Pend(reason, CalendarSyncSnapshots.Json(remote), local.Fingerprint); return Result.Success; }

    private static bool Equivalent(LocalCalendarSnapshot local, RemoteCalendarEvent remote) => local.StartsAt == remote.StartsAt
        && local.EndsAt == remote.EndsAt && local.Day == remote.Day && local.IsCancelled == remote.IsCancelled && !remote.IsRecurring;

    private static bool Converged(LocalCalendarSnapshot local, RemoteCalendarEvent remote, RemoteCalendarEvent? baseline) =>
        baseline is not null && Equivalent(local, remote) && string.Equals(remote.Title, baseline.Title, StringComparison.Ordinal)
        && string.Equals(remote.Description, baseline.Description, StringComparison.Ordinal) && string.Equals(remote.Location, baseline.Location, StringComparison.Ordinal);

    private bool Overlaps(CalendarConnection connection, DateTimeOffset startsAt, DateTimeOffset endsAt, DateOnly? day)
    {
        if (day is { } civil) { return civil >= connection.From && civil < connection.Until; }
        var from = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(connection.From.ToDateTime(TimeOnly.MinValue), clock.LegalTimeZone));
        var until = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(connection.Until.ToDateTime(TimeOnly.MinValue), clock.LegalTimeZone));
        return startsAt < until && endsAt > from;
    }
}

using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.CalendarSync;

internal sealed partial class CalendarSyncEngine
{
    public async Task<CalendarPendingResponse?> PendingAsync(CalendarConnection connection, CalendarSyncLink link, CancellationToken ct)
    {
        if (link.PendingSnapshot is null || link.PendingReason is null) { return null; }
        var local = await LocalAsync(connection, link, ct);
        if (link.LocalId is not null && local is null) { return null; }
        var remote = CalendarSyncSnapshots.Read(link.PendingSnapshot);
        return remote is null ? null : new CalendarPendingResponse(link.Id, link.Kind, link.LocalId, link.PendingReason.Value,
            local?.Title, local?.StartsAt, local?.EndsAt, remote.Title, remote.StartsAt, remote.EndsAt, remote.IsCancelled,
            local is { Kind: CalendarItemKind.Event, IsCancelled: false } && link.PendingReason != CalendarPendingReason.Unsupported
                && (remote.IsCancelled || (!remote.IsRecurring && remote.Day is null))
                && await access.CanEditEventAsync(connection.UserId, local.CaseId, ct));
    }

    public async Task<Result<CalendarPendingResolutionResponse>> ResolveAsync(Guid userId, ResolveCalendarPendingCommand command, CancellationToken ct)
    {
        if (!Enum.IsDefined(command.Resolution)) { return CalendarSyncErrors.InvalidRemote; }
        await db.LockDeadlinesAsync(ct);
        var connection = await db.CalendarConnections.SingleOrDefaultAsync(c => c.Id == command.ConnectionId && c.UserId == userId, ct);
        if (connection is null) { return CalendarSyncErrors.NotFound; }
        if (!connection.IsActive) { return CalendarSyncErrors.Inactive; }
        if (!await access.CanConnectAsync(userId, ct)) { return CalendarSyncErrors.AccessUnavailable; }
        var link = await db.CalendarSyncLinks.SingleOrDefaultAsync(l => l.Id == command.PendingId && l.ConnectionId == connection.Id, ct);
        if (link?.PendingReason is null || link.PendingSnapshot is null) { return CalendarSyncErrors.PendingNotFound; }
        var local = await LocalAsync(connection, link, ct);
        if (link.LocalId is not null && local is null) { return CalendarSyncErrors.PendingNotFound; }
        if (local is null && !await access.CanEditEventAsync(userId, null, ct)) { return CalendarSyncErrors.PendingNotFound; }
        var found = await provider.GetAsync(Context(connection), link.RemoteId, ct);
        if (found.IsFailure) { return found.Error; }
        var pending = CalendarSyncSnapshots.Read(link.PendingSnapshot)!;
        var remote = found.Value ?? pending with { Version = "missing", IsCancelled = true };
        if (remote != pending
            || !string.Equals(local?.Fingerprint ?? string.Empty, link.PendingLocalFingerprint, StringComparison.Ordinal))
        { return CalendarSyncErrors.StaleDecision; }
        var outcome = command.Resolution == CalendarResolution.KeepLocal
            ? await KeepLocalAsync(connection, link, local, remote, ct)
            : local is null ? CalendarSyncErrors.InvalidRemote : await ApplyAsync(connection, link, remote, CalendarSyncSnapshots.Read(link.RemoteSnapshot), ct);
        if (outcome.IsFailure) { return outcome.Error; }
        await db.SaveChangesAsync(ct);
        return new CalendarPendingResolutionResponse(link.Id, link.PendingReason is null);
    }

    private async Task<Result> KeepLocalAsync(CalendarConnection connection, CalendarSyncLink link, LocalCalendarSnapshot? local,
        RemoteCalendarEvent remote, CancellationToken ct)
    {
        if (local is not null) { return await PushAsync(connection, link, local, remote, ct); }
        link.Remember(remote.Version, CalendarSyncSnapshots.Json(remote), string.Empty);
        return Result.Success;
    }
}

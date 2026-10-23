using LexFlow.Application.Abstractions;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.CalendarSync;

internal sealed partial class CalendarSyncEngine(ISchedulingDbContext db, ICalendarSyncProvider provider,
    CalendarSyncAccess access, ITenantContext tenant, IClock clock, CalendarSyncSettings settings)
{
    private const int MaximumItems = 1000;

    public async Task RunAsync(Guid connectionId, Guid userId, CancellationToken ct)
    {
        await db.LockDeadlinesAsync(ct);
        var connection = await db.CalendarConnections.SingleOrDefaultAsync(c => c.Id == connectionId && c.UserId == userId, ct);
        if (connection is null || !connection.IsActive) { return; }
        var outcome = await SynchronizeAsync(connection, ct);
        if (outcome.IsSuccess) { connection.Succeeded(clock.UtcNow); }
        else { connection.Failed(outcome.Error.Code); }
        await db.SaveChangesAsync(ct);
    }

    private CalendarSyncContext Context(CalendarConnection connection) => new(tenant.RequiredOrganizationId, connection.UserId, connection.Provider);

    private async Task<Result> SynchronizeAsync(CalendarConnection connection, CancellationToken ct)
    {
        if (!provider.IsEnabled) { return CalendarSyncErrors.ProviderUnavailable; }
        if (!await access.CanConnectAsync(connection.UserId, ct)) { return CalendarSyncErrors.AccessUnavailable; }
        var remote = await ReadPagesAsync(connection, ct);
        if (remote.IsFailure) { return remote.Error; }
        var links = await db.CalendarSyncLinks.Where(l => l.ConnectionId == connection.Id).ToListAsync(ct);
        if (links.Count > MaximumItems) { return CalendarSyncErrors.SyncLimit; }
        foreach (var link in links)
        {
            var result = await SynchronizeLinkAsync(connection, link, ct);
            if (result.IsFailure) { return result; }
        }
        foreach (var entry in remote.Value.Where(r => links.All(l => !string.Equals(l.RemoteId, r.Id, StringComparison.Ordinal))))
        {
            var result = await ImportAsync(connection, entry, ct);
            if (result.IsFailure) { return result; }
        }
        await db.SaveChangesAsync(ct);
        return await ExportNewAsync(connection, ct);
    }

    private async Task<Result<IReadOnlyList<RemoteCalendarEvent>>> ReadPagesAsync(CalendarConnection connection, CancellationToken ct)
    {
        var entries = new Dictionary<string, RemoteCalendarEvent>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? cursor = null;
        do
        {
            var page = await provider.ReadAsync(Context(connection), connection.From, connection.Until, cursor, ct);
            if (page.IsFailure) { return page.Error; }
            foreach (var item in page.Value.Items)
            {
                if (!CalendarSyncSnapshots.Valid(item)) { return CalendarSyncErrors.InvalidRemote; }
                entries[item.Id] = item;
            }
            if (entries.Count > MaximumItems) { return CalendarSyncErrors.SyncLimit; }
            cursor = page.Value.NextCursor;
            if (cursor is not null && (!seen.Add(cursor) || seen.Count > 20)) { return CalendarSyncErrors.SyncLimit; }
        } while (cursor is not null);
        return entries.Values.ToArray();
    }

    private async Task<LocalCalendarSnapshot?> LocalAsync(CalendarConnection connection, CalendarSyncLink link, CancellationToken ct)
    {
        if (link.Kind == CalendarItemKind.Event)
        {
            var item = await db.Events.SingleOrDefaultAsync(e => e.Id == link.LocalId && e.AssigneeUserId == connection.UserId, ct);
            return item is null || !await access.CanUseAsync(connection.UserId, item.LegalCaseId, "schedule.events.read", ct) ? null : Snapshot(item);
        }
        var deadline = await db.Deadlines.SingleOrDefaultAsync(d => d.Id == link.LocalId && d.AssigneeUserId == connection.UserId, ct);
        return deadline is null || !await access.CanUseAsync(connection.UserId, deadline.LegalCaseId, "schedule.deadlines.read", ct) ? null : Snapshot(deadline);
    }

    private static LocalCalendarSnapshot Snapshot(CalendarEvent item) => new(item.Id, item.LegalCaseId, CalendarItemKind.Event,
        item.Title, item.Description, item.Location, item.StartsAt, item.EndsAt, null, item.Status == CalendarEventState.Cancelled, CalendarSyncSnapshots.Fingerprint(item));

    private LocalCalendarSnapshot Snapshot(Deadline item)
    {
        var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(item.FatalDueOn.ToDateTime(TimeOnly.MinValue), clock.LegalTimeZone));
        var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(item.FatalDueOn.AddDays(1).ToDateTime(TimeOnly.MinValue), clock.LegalTimeZone));
        return new(item.Id, item.LegalCaseId, CalendarItemKind.Deadline, item.Title, null, null, start, end, item.FatalDueOn,
            item.Status is WorkItemStatus.Completed or WorkItemStatus.Missed, CalendarSyncSnapshots.Fingerprint(item));
    }
}

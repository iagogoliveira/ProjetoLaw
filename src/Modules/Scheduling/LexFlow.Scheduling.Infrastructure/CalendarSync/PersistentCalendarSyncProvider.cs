using LexFlow.Application.Abstractions;
using LexFlow.Scheduling.Application.CalendarSync;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.Scheduling.Infrastructure.Persistence;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Infrastructure.CalendarSync;

internal sealed class PersistentCalendarSyncProvider(SchedulingDbContext db, ITenantContext tenant, FakeCalendarSyncOptions options) : ICalendarSyncProvider
{
    public bool IsEnabled => options.Enabled;
    public bool IsSimulation => true;

    public async Task<Result<RemoteCalendarPage>> ReadAsync(CalendarSyncContext context, DateOnly from, DateOnly until, string? cursor, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(context, cancellationToken);
        return provider.IsFailure ? provider.Error : await provider.Value.ReadAsync(context, from, until, cursor, cancellationToken);
    }

    public async Task<Result<RemoteCalendarEvent?>> GetAsync(CalendarSyncContext context, string remoteId, CancellationToken cancellationToken)
    {
        if (!Allowed(context)) { return CalendarSyncErrors.ProviderUnavailable; }
        var item = await db.SimulatedCalendarItems.AsNoTracking().SingleOrDefaultAsync(e => e.UserId == context.UserId
            && e.Provider == context.Provider && e.RemoteId == remoteId, cancellationToken);
        return item is null ? Result.Ok<RemoteCalendarEvent?>(null) : CalendarSyncSnapshots.Read(item.Payload);
    }

    public async Task<Result<RemoteCalendarEvent>> WriteAsync(CalendarSyncContext context, CalendarSyncWrite item, string? remoteId,
        string? expectedVersion, CancellationToken cancellationToken)
    {
        var provider = await LoadAsync(context, cancellationToken);
        if (provider.IsFailure) { return provider.Error; }
        if (!db.HasActiveTransaction) { throw new InvalidOperationException("O simulador persistente exige a transação de Scheduling."); }
        var result = await provider.Value.WriteAsync(context, item, remoteId, expectedVersion, cancellationToken);
        if (result.IsFailure) { return result.Error; }
        var existing = await db.SimulatedCalendarItems.SingleOrDefaultAsync(e => e.UserId == context.UserId
            && e.Provider == context.Provider && e.RemoteId == result.Value.Id, cancellationToken);
        if (existing is null)
        {
            db.SimulatedCalendarItems.Add(SimulatedCalendarItem.Create(context.UserId, context.Provider, result.Value.Id,
                result.Value.ClientKey, CalendarSyncSnapshots.Json(result.Value), item.Link));
        }
        else { existing.Update(CalendarSyncSnapshots.Json(result.Value), item.Link); }
        await db.SaveChangesAsync(cancellationToken);
        return result.Value;
    }

    private bool Allowed(CalendarSyncContext context) => options.Enabled && options.Available
        && context.OrganizationId == tenant.OrganizationId && context.UserId != Guid.Empty && Enum.IsDefined(context.Provider);

    private async Task<Result<FakeCalendarSyncProvider>> LoadAsync(CalendarSyncContext context, CancellationToken ct)
    {
        if (!Allowed(context)) { return CalendarSyncErrors.ProviderUnavailable; }
        var rows = await db.SimulatedCalendarItems.AsNoTracking().Where(e => e.UserId == context.UserId && e.Provider == context.Provider)
            .OrderBy(e => e.Id).Take(10001).ToListAsync(ct);
        if (rows.Count > 10000) { return CalendarSyncErrors.SyncLimit; }
        var provider = new FakeCalendarSyncProvider(options);
        provider.Seed(context, rows.Select(e => CalendarSyncSnapshots.Read(e.Payload)!).ToArray());
        return provider;
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.CalendarSync;

public sealed record CalendarConnectionResponse(Guid Id, CalendarProvider Provider, DateOnly From, DateOnly Until,
    bool IsActive, DateTimeOffset? LastSyncedAt, string? LastErrorCode)
{
    public static CalendarConnectionResponse Of(CalendarConnection connection) => new(connection.Id, connection.Provider,
        connection.From, connection.Until, connection.IsActive, connection.LastSyncedAt, connection.LastErrorCode);
}
public sealed record CalendarConnectionsResponse(bool IsSimulation, bool IsAvailable, IReadOnlyList<CalendarConnectionResponse> Items);
public sealed record ListCalendarConnectionsQuery : IQuery<CalendarConnectionsResponse>;
public sealed record ConnectCalendarCommand(CalendarProvider Provider, DateOnly From, DateOnly Until) : ICommand<CalendarConnectionResponse>;
public sealed record DisconnectCalendarCommand(Guid ConnectionId) : ICommand<CalendarConnectionResponse>;
public sealed record QueueCalendarSyncCommand(Guid ConnectionId) : ICommand<CalendarConnectionResponse>;
public interface ICalendarSyncScheduler { void Enqueue(Guid organizationId, Guid userId, Guid connectionId); }

internal sealed class CalendarConnectionAuthorizer(ISchedulingDbContext db, ICurrentUser user)
    : IResourceAuthorizer<DisconnectCalendarCommand>, IResourceAuthorizer<QueueCalendarSyncCommand>
{
    public Task<Result> AuthorizeAsync(DisconnectCalendarCommand request, CancellationToken ct) => OwnAsync(request.ConnectionId, ct);
    public Task<Result> AuthorizeAsync(QueueCalendarSyncCommand request, CancellationToken ct) => OwnAsync(request.ConnectionId, ct);
    private async Task<Result> OwnAsync(Guid id, CancellationToken ct) =>
        await db.CalendarConnections.AnyAsync(c => c.Id == id && c.UserId == user.UserId, ct) ? Result.Success : CalendarSyncErrors.NotFound;
}

internal sealed class CalendarConnectionQueries(ISchedulingDbContext db, ICurrentUser user, ICalendarSyncProvider provider)
    : IQueryHandler<ListCalendarConnectionsQuery, CalendarConnectionsResponse>
{
    public async Task<Result<CalendarConnectionsResponse>> Handle(ListCalendarConnectionsQuery query, CancellationToken ct) =>
        new CalendarConnectionsResponse(provider.IsSimulation, provider.IsEnabled, (await db.CalendarConnections.AsNoTracking()
            .Where(c => c.UserId == user.UserId).OrderBy(c => c.Provider).ToListAsync(ct)).Select(CalendarConnectionResponse.Of).ToArray());
}

internal sealed class ConnectCalendarHandler(ISchedulingDbContext db, ICurrentUser user, ICalendarSyncProvider provider)
    : ICommandHandler<ConnectCalendarCommand, CalendarConnectionResponse>
{
    public async Task<Result<CalendarConnectionResponse>> Handle(ConnectCalendarCommand command, CancellationToken ct)
    {
        var candidate = CalendarConnection.Connect(user.UserId ?? Guid.Empty, command.Provider, command.From, command.Until);
        if (candidate.IsFailure) { return candidate.Error; }
        if (!provider.IsEnabled) { return CalendarSyncErrors.ProviderUnavailable; }
        await db.LockDeadlinesAsync(ct);
        var existing = await db.CalendarConnections.SingleOrDefaultAsync(c => c.UserId == user.UserId && c.Provider == command.Provider, ct);
        if (existing is null) { existing = candidate.Value; db.CalendarConnections.Add(existing); }
        else { existing.Activate(command.From, command.Until); }
        await db.SaveChangesAsync(ct);
        return CalendarConnectionResponse.Of(existing);
    }
}

internal sealed class CalendarConnectionMutations(ISchedulingDbContext db, ICurrentUser user, ITenantContext tenant,
    ICalendarSyncScheduler jobs, ICalendarSyncProvider provider) : ICommandHandler<DisconnectCalendarCommand, CalendarConnectionResponse>,
    ICommandHandler<QueueCalendarSyncCommand, CalendarConnectionResponse>
{
    public async Task<Result<CalendarConnectionResponse>> Handle(DisconnectCalendarCommand command, CancellationToken ct)
    {
        await db.LockDeadlinesAsync(ct);
        var connection = await db.CalendarConnections.SingleOrDefaultAsync(c => c.Id == command.ConnectionId && c.UserId == user.UserId, ct);
        if (connection is null) { return CalendarSyncErrors.NotFound; }
        connection.Disconnect();
        await db.SaveChangesAsync(ct);
        return CalendarConnectionResponse.Of(connection);
    }

    public async Task<Result<CalendarConnectionResponse>> Handle(QueueCalendarSyncCommand command, CancellationToken ct)
    {
        var connection = await db.CalendarConnections.SingleOrDefaultAsync(c => c.Id == command.ConnectionId && c.UserId == user.UserId, ct);
        if (connection is null) { return CalendarSyncErrors.NotFound; }
        if (!connection.IsActive) { return CalendarSyncErrors.Inactive; }
        if (!provider.IsEnabled) { return CalendarSyncErrors.ProviderUnavailable; }
        jobs.Enqueue(tenant.RequiredOrganizationId, connection.UserId, connection.Id);
        return CalendarConnectionResponse.Of(connection);
    }
}

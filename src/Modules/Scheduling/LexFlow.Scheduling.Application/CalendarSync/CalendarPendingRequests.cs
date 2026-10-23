using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application.CalendarSync;

public sealed record CalendarPendingResponse(Guid Id, CalendarItemKind Kind, Guid? LocalId, CalendarPendingReason Reason,
    string? LocalTitle, DateTimeOffset? LocalStartsAt, DateTimeOffset? LocalEndsAt, string ExternalTitle,
    DateTimeOffset ExternalStartsAt, DateTimeOffset ExternalEndsAt, bool ExternalCancelled, bool CanAcceptExternal);
public sealed record CalendarPendingPage(IReadOnlyList<CalendarPendingResponse> Items, Guid? NextCursor);
public sealed record ListCalendarPendingQuery(Guid ConnectionId, Guid? After = null) : IQuery<CalendarPendingPage>;
public sealed record ResolveCalendarPendingCommand(Guid ConnectionId, Guid PendingId, CalendarResolution Resolution) : ICommand<CalendarPendingResolutionResponse>;
public sealed record CalendarPendingResolutionResponse(Guid PendingId, bool Resolved);

internal sealed class CalendarPendingAuthorizers(ISchedulingDbContext db, ICurrentUser user, AgendaAccess access)
    : IResourceAuthorizer<ListCalendarPendingQuery>, IResourceAuthorizer<ResolveCalendarPendingCommand>
{
    public async Task<Result> AuthorizeAsync(ListCalendarPendingQuery request, CancellationToken ct) =>
        await db.CalendarConnections.AnyAsync(c => c.Id == request.ConnectionId && c.UserId == user.UserId, ct) ? Result.Success : CalendarSyncErrors.NotFound;

    public async Task<Result> AuthorizeAsync(ResolveCalendarPendingCommand request, CancellationToken ct)
    {
        var link = await db.CalendarSyncLinks.AsNoTracking().SingleOrDefaultAsync(l => l.Id == request.PendingId && l.ConnectionId == request.ConnectionId
            && db.CalendarConnections.Any(c => c.Id == l.ConnectionId && c.UserId == user.UserId), ct);
        if (link is null) { return CalendarSyncErrors.PendingNotFound; }
        if (link.Kind == CalendarItemKind.Event && link.LocalId is { } localId)
        {
            var permission = request.Resolution == CalendarResolution.AcceptExternal ? "schedule.events.manage" : "schedule.events.read";
            var allowed = await access.EventAsync(localId, permission, ct);
            if (allowed.IsFailure) { return CalendarSyncErrors.PendingNotFound; }
        }
        return Result.Success;
    }
}

internal sealed class CalendarPendingHandlers(ISchedulingDbContext db, ICurrentUser user, CalendarSyncEngine engine, CalendarSyncAccess access)
    : IQueryHandler<ListCalendarPendingQuery, CalendarPendingPage>, ICommandHandler<ResolveCalendarPendingCommand, CalendarPendingResolutionResponse>
{
    public async Task<Result<CalendarPendingPage>> Handle(ListCalendarPendingQuery query, CancellationToken ct)
    {
        var connection = await db.CalendarConnections.AsNoTracking().SingleOrDefaultAsync(c => c.Id == query.ConnectionId && c.UserId == user.UserId, ct);
        if (connection is null) { return CalendarSyncErrors.NotFound; }
        var events = await access.ScopeAsync(connection.UserId, "schedule.events.read", ct);
        var deadlines = await access.ScopeAsync(connection.UserId, "schedule.deadlines.read", ct);
        var links = await db.CalendarSyncLinks.AsNoTracking().Where(l => l.ConnectionId == connection.Id && l.PendingReason != null
            && (l.Kind == CalendarItemKind.Event ? l.LegalCaseId != null ? events.Cases.Contains(l.LegalCaseId.Value) : events.AllAdministrative
                : l.LegalCaseId != null && deadlines.Cases.Contains(l.LegalCaseId.Value))
            && (query.After == null || l.Id.CompareTo(query.After.Value) > 0)).OrderBy(l => l.Id).Take(101).ToListAsync(ct);
        var result = new List<CalendarPendingResponse>();
        foreach (var link in links.Take(100))
        {
            var projection = await engine.PendingAsync(connection, link, ct);
            if (projection is not null) { result.Add(projection); }
        }
        return new CalendarPendingPage(result, links.Count > 100 ? links[99].Id : null);
    }

    public Task<Result<CalendarPendingResolutionResponse>> Handle(ResolveCalendarPendingCommand command, CancellationToken ct) =>
        engine.ResolveAsync(user.UserId ?? Guid.Empty, command, ct);
}

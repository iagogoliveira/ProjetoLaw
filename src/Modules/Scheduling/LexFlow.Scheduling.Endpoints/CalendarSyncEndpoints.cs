using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Application.CalendarSync;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Scheduling.Endpoints;

public sealed record ResolveCalendarPendingRequest(CalendarResolution Resolution);

public static class CalendarSyncEndpoints
{
    private const string Route = "/api/v1/me/calendar-connections";

    public static void MapCalendarSync(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Route).WithTags("Scheduling");
        group.MapGet("", async (IQueryHandler<ListCalendarConnectionsQuery, CalendarConnectionsResponse> handler, CancellationToken ct) =>
                (await handler.Handle(new ListCalendarConnectionsQuery(), ct)).ToHttpResult())
            .RequirePermission("identity.profile.read").WithName("ListCalendarConnections").Produces<CalendarConnectionsResponse>();
        group.MapPost("", async (ConnectCalendarCommand body, ICommandHandler<ConnectCalendarCommand, CalendarConnectionResponse> handler, CancellationToken ct) =>
                (await handler.Handle(body, ct)).ToHttpResult())
            .RequirePermission("identity.profile.manage").WithName("ConnectCalendar").Produces<CalendarConnectionResponse>();
        group.MapPost("/{id:guid}/disconnect", async (Guid id, ICommandHandler<DisconnectCalendarCommand, CalendarConnectionResponse> handler, CancellationToken ct) =>
                (await handler.Handle(new DisconnectCalendarCommand(id), ct)).ToHttpResult())
            .RequirePermission("identity.profile.manage").WithName("DisconnectCalendar").Produces<CalendarConnectionResponse>();
        group.MapPost("/{id:guid}/sync", async (Guid id, ICommandHandler<QueueCalendarSyncCommand, CalendarConnectionResponse> handler, CancellationToken ct) =>
                (await handler.Handle(new QueueCalendarSyncCommand(id), ct)).ToHttpResult(item => TypedResults.Accepted($"{Route}", item)))
            .RequirePermission("identity.profile.manage").WithName("QueueCalendarSync").Produces<CalendarConnectionResponse>(StatusCodes.Status202Accepted);
        group.MapGet("/{id:guid}/pending", async (Guid id, Guid? after, IQueryHandler<ListCalendarPendingQuery, CalendarPendingPage> handler, CancellationToken ct) =>
                (await handler.Handle(new ListCalendarPendingQuery(id, after), ct)).ToHttpResult())
            .RequirePermission("identity.profile.read").WithName("ListCalendarPending").Produces<CalendarPendingPage>();
        group.MapPost("/{id:guid}/pending/{pendingId:guid}/resolve", async (Guid id, Guid pendingId, ResolveCalendarPendingRequest body,
                ICommandHandler<ResolveCalendarPendingCommand, CalendarPendingResolutionResponse> handler, CancellationToken ct) =>
                (await handler.Handle(new ResolveCalendarPendingCommand(id, pendingId, body.Resolution), ct)).ToHttpResult())
            .RequirePermission("identity.profile.manage").WithName("ResolveCalendarPending").Produces<CalendarPendingResolutionResponse>();
    }
}

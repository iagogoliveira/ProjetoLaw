using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Application;
using LexFlow.Scheduling.Domain;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Scheduling.Endpoints;

public static class EventEndpoints
{
    private const string Route = "/api/v1/schedule/events";

    public static void MapEvents(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Route, async (CalendarEventInput input,
                ICommandHandler<RegisterEventCommand, CalendarEventResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new RegisterEventCommand(input), ct)).ToHttpResult(entry =>
                TypedResults.Created($"{Route}/{entry.Id:D}", entry)))
            .WithTags("Scheduling").RequirePermission("schedule.events.manage").WithName("RegisterEvent")
            .Produces<CalendarEventResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);

        var group = endpoints.MapGroup(Route).WithTags("Scheduling");
        group.MapGet("/{id:guid}", async (Guid id,
                IQueryHandler<GetEventQuery, CalendarEventResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetEventQuery(id), ct)).ToHttpResult())
            .RequirePermission("schedule.events.read").WithName("GetEvent").Produces<CalendarEventResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (Guid id, CalendarEventInput input,
                ICommandHandler<EditEventCommand, CalendarEventResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new EditEventCommand(id, input), ct)).ToHttpResult())
            .RequirePermission("schedule.events.manage").WithName("EditEvent").Produces<CalendarEventResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", async (Guid id,
                ICommandHandler<CancelEventCommand, CalendarEventResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new CancelEventCommand(id), ct)).ToHttpResult())
            .RequirePermission("schedule.events.manage").WithName("CancelEvent").Produces<CalendarEventResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

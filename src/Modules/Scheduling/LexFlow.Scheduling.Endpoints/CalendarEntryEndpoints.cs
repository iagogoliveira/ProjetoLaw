using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Application.Calendar;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Scheduling.Endpoints;

public static class CalendarEntryEndpoints
{
    private const string Route = "/api/v1/schedule/calendar-entries";

    /// <summary>Dias sem expediente do escritório: feriados próprios, suspensões e indisponibilidades do sistema do tribunal.</summary>
    public static void MapCalendarEntries(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/schedule").WithTags("Scheduling");
        group.MapGet("/calendar-entries", async (IQueryHandler<ListCalendarEntriesQuery, IReadOnlyList<CalendarEntryResponse>> handler, CancellationToken ct) =>
            (await handler.Handle(new ListCalendarEntriesQuery(), ct)).ToHttpResult())
            .RequirePermission("schedule.deadlines.read").WithName("ListCalendarEntries")
            .WithSummary("Dias sem expediente cadastrados pelo escritório").Produces<IReadOnlyList<CalendarEntryResponse>>();
        group.MapPost("/calendar-entries", async (CalendarEntryInput input, ICommandHandler<CreateCalendarEntryCommand, CalendarEntryResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new CreateCalendarEntryCommand(input), ct)).ToHttpResult(e => TypedResults.Created($"{Route}/{e.Id:D}", e)))
            .RequirePermission("schedule.calendar.manage").WithName("CreateCalendarEntry").Produces<CalendarEntryResponse>(StatusCodes.Status201Created);
        group.MapPut("/calendar-entries/{id:guid}", async (Guid id, CalendarEntryInput input, ICommandHandler<UpdateCalendarEntryCommand, CalendarEntryResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new UpdateCalendarEntryCommand(id, input), ct)).ToHttpResult())
            .RequirePermission("schedule.calendar.manage").WithName("UpdateCalendarEntry").Produces<CalendarEntryResponse>();
        group.MapDelete("/calendar-entries/{id:guid}", async (Guid id, ICommandHandler<DeleteCalendarEntryCommand, Unit> handler, CancellationToken ct) =>
            (await handler.Handle(new DeleteCalendarEntryCommand(id), ct)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission("schedule.calendar.manage").WithName("DeleteCalendarEntry").Produces(StatusCodes.Status204NoContent);
    }
}

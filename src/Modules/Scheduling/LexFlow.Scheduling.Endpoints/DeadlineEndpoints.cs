using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Application;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Scheduling.Endpoints;

public sealed record CompleteDeadlineBody(string Evidence);
public sealed record MissDeadlineBody(string Reason);

public static class DeadlineEndpoints
{
    public static void MapDeadlines(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/cases/{id:guid}/deadlines", async (Guid id, ManualDeadlineRequest input,
                ICommandHandler<RegisterDeadlineCommand, DeadlineDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new RegisterDeadlineCommand(id, input), ct)).ToHttpResult(d => TypedResults.Created($"/api/v1/deadlines/{d.Id:D}", d)))
            .WithTags("Scheduling").RequirePermission("schedule.deadlines.manage").WithName("RegisterDeadline")
            .Produces<DeadlineDetailResponse>(StatusCodes.Status201Created);
        var group = endpoints.MapGroup("/api/v1/deadlines").WithTags("Scheduling");
        group.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetDeadlineQuery, DeadlineDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetDeadlineQuery(id), ct)).ToHttpResult())
            .RequirePermission("schedule.deadlines.read").WithName("GetDeadline").Produces<DeadlineDetailResponse>();
        MapTransitions(group);
    }

    private static void MapTransitions(RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/start", async (Guid id, ICommandHandler<StartDeadlineCommand, DeadlineDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new StartDeadlineCommand(id), ct)).ToHttpResult())
            .RequirePermission("schedule.deadlines.manage").WithName("StartDeadline").Produces<DeadlineDetailResponse>();
        group.MapPost("/{id:guid}/review", async (Guid id, ICommandHandler<ReviewDeadlineCommand, DeadlineDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new ReviewDeadlineCommand(id), ct)).ToHttpResult())
            .RequirePermission("schedule.deadlines.verify").WithName("ReviewDeadline").Produces<DeadlineDetailResponse>();
        group.MapPost("/{id:guid}/complete", async (Guid id, CompleteDeadlineBody body,
                ICommandHandler<CompleteDeadlineCommand, DeadlineDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new CompleteDeadlineCommand(id, body.Evidence), ct)).ToHttpResult())
            .RequirePermission("schedule.deadlines.manage").WithName("CompleteDeadline").Produces<DeadlineDetailResponse>();
        group.MapPost("/{id:guid}/miss", async (Guid id, MissDeadlineBody body,
                ICommandHandler<MissDeadlineCommand, DeadlineDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new MissDeadlineCommand(id, body.Reason), ct)).ToHttpResult())
            .RequirePermission("schedule.deadlines.manage").WithName("MissDeadline").Produces<DeadlineDetailResponse>();
    }
}

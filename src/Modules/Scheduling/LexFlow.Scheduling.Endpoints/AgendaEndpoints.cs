using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Application;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Scheduling.Endpoints;

public static class AgendaEndpoints
{
    public static void MapAgenda(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/schedule/agenda", async (DateOnly from, DateOnly until, Guid? assigneeUserId, Guid? teamId,
            bool? includeClosed, string? cursor, int? limit, IQueryHandler<ListAgendaQuery, AgendaPage> handler, CancellationToken ct) =>
            (await handler.Handle(new ListAgendaQuery(from, until, assigneeUserId, teamId, includeClosed ?? false, cursor, limit ?? 50), ct)).ToHttpResult())
            .WithTags("Scheduling").RequirePermission("schedule.events.read").WithName("ListAgenda").Produces<AgendaPage>();
        MapRedistribution(endpoints, "/api/v1/tasks/redistribute", AgendaKind.Task, "schedule.tasks.manage", "RedistributeTasks");
        MapRedistribution(endpoints, "/api/v1/deadlines/redistribute", AgendaKind.Deadline, "schedule.deadlines.manage", "RedistributeDeadlines");
        MapRedistribution(endpoints, "/api/v1/schedule/events/redistribute", AgendaKind.Event, "schedule.events.manage", "RedistributeEvents");
    }

    private static void MapRedistribution(IEndpointRouteBuilder endpoints, string path, AgendaKind kind, string permission, string name) =>
        endpoints.MapPost(path, async (RedistributionInput input, ICommandHandler<RedistributeWorkCommand, RedistributionResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new RedistributeWorkCommand(kind, input), ct)).ToHttpResult())
            .WithTags("Scheduling").RequirePermission(permission).WithName(name).Produces<RedistributionResponse>();
}

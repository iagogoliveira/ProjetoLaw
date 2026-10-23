using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Application;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Scheduling.Endpoints;

public static class SchedulingOptionsEndpoints
{
    public static void MapSchedulingOptions(this IEndpointRouteBuilder endpoints)
    {
        Map(endpoints, "/api/v1/schedule/options", "schedule.events.read", "GetSchedulingOptions");
        Map(endpoints, "/api/v1/tasks/options", "schedule.tasks.read", "GetTaskOptions");
    }

    private static void Map(IEndpointRouteBuilder endpoints, string path, string permission, string name) =>
        endpoints.MapGet(path, async (IQueryHandler<GetSchedulingOptionsQuery, SchedulingOptionsResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetSchedulingOptionsQuery(), ct)).ToHttpResult())
            .WithTags("Scheduling").RequirePermission(permission).WithName(name).Produces<SchedulingOptionsResponse>();
}

using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Application;
using LexFlow.Scheduling.Application.Calendar;
using LexFlow.Web.Common.Modules;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Scheduling.Endpoints;

public sealed class SchedulingModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) { }
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/cases").WithTags("Scheduling");
        group.MapGet("/{id:guid}/deadlines", async (Guid id, IQueryHandler<ListCaseDeadlinesQuery, IReadOnlyList<DeadlineResponse>> handler, CancellationToken ct) =>
            (await handler.Handle(new ListCaseDeadlinesQuery(id), ct)).ToHttpResult())
            .RequirePermission("schedule.deadlines.read").WithName("ListCaseDeadlines").Produces<IReadOnlyList<DeadlineResponse>>();
        group.MapGet("/{id:guid}/tasks", async (Guid id, IQueryHandler<ListCaseTasksQuery, IReadOnlyList<TaskItemResponse>> handler, CancellationToken ct) =>
            (await handler.Handle(new ListCaseTasksQuery(id), ct)).ToHttpResult())
            .RequirePermission("schedule.tasks.read").WithName("ListCaseTasks").Produces<IReadOnlyList<TaskItemResponse>>();

        endpoints.MapPost("/api/v1/deadlines/calculate", async (CalculateDeadlineQuery body,
                IQueryHandler<CalculateDeadlineQuery, DeadlineCalculationResponse> handler, CancellationToken ct) =>
            (await handler.Handle(body, ct)).ToHttpResult())
            .WithTags("Scheduling").RequirePermission("schedule.deadlines.manage").WithName("CalculateDeadline")
            .WithSummary("Sugere o vencimento de um prazo, com a memória de cálculo; não grava nada.")
            .Produces<DeadlineCalculationResponse>();
        endpoints.MapCalendarEntries();
        endpoints.MapDeadlines();
    }
}

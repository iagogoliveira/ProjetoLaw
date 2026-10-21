using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Application;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Queries;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Scheduling.Endpoints;

public sealed record ChangeTaskStateBody(TaskState Status);
public sealed record AddTaskChecklistBody(string Title);
public sealed record SetTaskChecklistBody(bool Completed);
public sealed record AddTaskCommentBody(string Text);

public static class TaskEndpoints
{
    public static void MapTasks(this IEndpointRouteBuilder endpoints)
    {
        MapCollection(endpoints);
        MapTaskDetails(endpoints);
        MapTaskEditing(endpoints);
        MapTaskActions(endpoints);
    }

    private static void MapCollection(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/tasks", async (TaskInput input,
                ICommandHandler<RegisterTaskCommand, TaskDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new RegisterTaskCommand(input), ct)).ToHttpResult(task =>
                TypedResults.Created($"/api/v1/tasks/{task.Id:D}", task)))
            .WithTags("Scheduling").RequirePermission("schedule.tasks.manage").WithName("RegisterTask")
            .Produces<TaskDetailResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/v1/tasks", ListAsync)
            .DocumentEnumQuery<TaskState>("status").DocumentEnumQuery<TaskPriority>("priority")
            .WithTags("Scheduling").RequirePermission("schedule.tasks.read").WithName("ListTasks")
            .Produces<TaskListResponse>().ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static void MapTaskDetails(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tasks").WithTags("Scheduling");
        group.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetTaskQuery, TaskDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetTaskQuery(id), ct)).ToHttpResult())
            .RequirePermission("schedule.tasks.read").WithName("GetTask").Produces<TaskDetailResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapTaskEditing(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tasks").WithTags("Scheduling");
        group.MapPut("/{id:guid}", async (Guid id, TaskInput input,
                ICommandHandler<EditTaskCommand, TaskDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new EditTaskCommand(id, input), ct)).ToHttpResult())
            .RequirePermission("schedule.tasks.manage").WithName("EditTask").Produces<TaskDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapTaskActions(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tasks").WithTags("Scheduling");
        group.MapPost("/{id:guid}/subtasks", async (Guid id, TaskInput input,
                ICommandHandler<RegisterTaskCommand, TaskDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new RegisterTaskCommand(input, id), ct)).ToHttpResult(task =>
                TypedResults.Created($"/api/v1/tasks/{task.Id:D}", task)))
            .RequirePermission("schedule.tasks.manage").WithName("RegisterSubtask").Produces<TaskDetailResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/state", async (Guid id, ChangeTaskStateBody body,
                ICommandHandler<ChangeTaskStateCommand, TaskDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new ChangeTaskStateCommand(id, body.Status), ct)).ToHttpResult())
            .RequirePermission("schedule.tasks.manage").WithName("ChangeTaskState").Produces<TaskDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/checklist", async (Guid id, AddTaskChecklistBody body,
                ICommandHandler<AddChecklistCommand, TaskDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new AddChecklistCommand(id, body.Title), ct)).ToHttpResult())
            .RequirePermission("schedule.tasks.manage").WithName("AddTaskChecklistItem").Produces<TaskDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/checklist/{itemId:guid}", async (Guid id, Guid itemId, SetTaskChecklistBody body,
                ICommandHandler<SetChecklistCommand, TaskDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new SetChecklistCommand(id, itemId, body.Completed), ct)).ToHttpResult())
            .RequirePermission("schedule.tasks.manage").WithName("SetTaskChecklistItem").Produces<TaskDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/comments", async (Guid id, AddTaskCommentBody body,
                ICommandHandler<AddTaskCommentCommand, TaskDetailResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new AddTaskCommentCommand(id, body.Text), ct)).ToHttpResult())
            .RequirePermission("schedule.tasks.manage").WithName("AddTaskComment").Produces<TaskDetailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> ListAsync(Guid? caseId, Guid? assigneeUserId, Guid? teamId, Guid? parentTaskId,
        string? status, string? priority, Guid? after, int? limit,
        IQueryHandler<ListTasksQuery, TaskListResponse> handler, CancellationToken ct)
    {
        var statusFilter = EnumQuery.Parse<TaskState>(status, SchedulingErrors.InvalidAgenda);
        if (statusFilter.IsFailure) { return statusFilter.Error.ToProblem(); }
        var priorityFilter = EnumQuery.Parse<TaskPriority>(priority, SchedulingErrors.InvalidAgenda);
        if (priorityFilter.IsFailure) { return priorityFilter.Error.ToProblem(); }

        return (await handler.Handle(new ListTasksQuery(caseId, assigneeUserId, teamId, parentTaskId,
            statusFilter.Value, priorityFilter.Value, after, limit ?? 50), ct)).ToHttpResult();
    }
}

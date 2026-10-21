using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Application;

public sealed record ChecklistResponse(Guid Id, string Title, bool Completed);
public sealed record TaskCommentResponse(Guid Id, string Text, Guid AuthorUserId, DateTimeOffset OccurredAt);
public sealed record TaskDetailResponse(Guid Id, Guid? CaseId, Guid? PublicationId, Guid? ParentTaskId, string Title,
    string? Description, DateOnly DueOn, Guid AssigneeUserId, TaskState Status, TaskPriority Priority,
    Guid? ClosedBy, DateTimeOffset? ClosedAt, IReadOnlyList<ChecklistResponse> Checklist, IReadOnlyList<TaskCommentResponse> Comments)
{
    public static TaskDetailResponse Of(TaskItem task) => new(task.Id, task.LegalCaseId, task.PublicationId, task.ParentTaskId,
        task.Title, task.Description, task.DueOn, task.AssigneeUserId, task.Status, task.Priority, task.ClosedBy, task.ClosedAt,
        task.Checklist.OrderBy(c => c.Id).Select(c => new ChecklistResponse(c.Id, c.Title, c.Completed)).ToArray(),
        task.Comments.OrderBy(c => c.OccurredAt).ThenBy(c => c.Id).Select(c => new TaskCommentResponse(c.Id, c.Text, c.AuthorUserId, c.OccurredAt)).ToArray());
}

public sealed record RegisterTaskCommand(TaskInput Input, Guid? ParentTaskId = null) : ICommand<TaskDetailResponse>;
public sealed record GetTaskQuery(Guid TaskId) : IQuery<TaskDetailResponse>;
public sealed record EditTaskCommand(Guid TaskId, TaskInput Input) : ICommand<TaskDetailResponse>;
public sealed record ChangeTaskStateCommand(Guid TaskId, TaskState Status) : ICommand<TaskDetailResponse>;
public sealed record AddChecklistCommand(Guid TaskId, string Title) : ICommand<TaskDetailResponse>;
public sealed record SetChecklistCommand(Guid TaskId, Guid ItemId, bool Completed) : ICommand<TaskDetailResponse>;
public sealed record AddTaskCommentCommand(Guid TaskId, string Text) : ICommand<TaskDetailResponse>;

internal sealed class TaskAuthorizers(AgendaAccess access) : IResourceAuthorizer<RegisterTaskCommand>, IResourceAuthorizer<GetTaskQuery>,
    IResourceAuthorizer<EditTaskCommand>, IResourceAuthorizer<ChangeTaskStateCommand>, IResourceAuthorizer<AddChecklistCommand>,
    IResourceAuthorizer<SetChecklistCommand>, IResourceAuthorizer<AddTaskCommentCommand>
{
    public async Task<Result> AuthorizeAsync(RegisterTaskCommand request, CancellationToken ct)
    {
        var allowed = await access.AuthorizeAsync(request.Input.CaseId, request.Input.AssigneeUserId, "schedule.tasks.manage", SchedulingErrors.TaskNotFound, ct);
        if (allowed.IsFailure || request.ParentTaskId is null) { return allowed; }
        return await access.TaskAsync(request.ParentTaskId.Value, "schedule.tasks.manage", ct);
    }
    public Task<Result> AuthorizeAsync(GetTaskQuery request, CancellationToken ct) => access.TaskAsync(request.TaskId, "schedule.tasks.read", ct);
    public Task<Result> AuthorizeAsync(EditTaskCommand request, CancellationToken ct) => access.TaskAsync(request.TaskId, "schedule.tasks.manage", ct);
    public Task<Result> AuthorizeAsync(ChangeTaskStateCommand request, CancellationToken ct) => access.TaskAsync(request.TaskId, "schedule.tasks.manage", ct);
    public Task<Result> AuthorizeAsync(AddChecklistCommand request, CancellationToken ct) => access.TaskAsync(request.TaskId, "schedule.tasks.manage", ct);
    public Task<Result> AuthorizeAsync(SetChecklistCommand request, CancellationToken ct) => access.TaskAsync(request.TaskId, "schedule.tasks.manage", ct);
    public Task<Result> AuthorizeAsync(AddTaskCommentCommand request, CancellationToken ct) => access.TaskAsync(request.TaskId, "schedule.tasks.manage", ct);
}

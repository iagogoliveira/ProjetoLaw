using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

internal sealed class TaskWrites(ISchedulingDbContext db, AgendaAccess access)
{
    public async Task<Result<TaskItem>> LoadAsync(Guid id, CancellationToken ct)
    {
        await db.LockDeadlinesAsync(ct);
        var allowed = await access.TaskAsync(id, "schedule.tasks.manage", ct);
        if (allowed.IsFailure) { return allowed.Error; }
        var task = await db.Tasks.Include(t => t.Checklist).Include(t => t.Comments).SingleAsync(t => t.Id == id, ct);
        if (task.ParentTaskId is { } parent && await db.Tasks.AnyAsync(t => t.Id == parent
            && (t.Status == TaskState.Completed || t.Status == TaskState.Cancelled), ct)) { return SchedulingErrors.TaskTransitionInvalid; }
        return task;
    }

    public async Task<Result> ValidateInputAsync(TaskInput input, CancellationToken ct)
    {
        var allowed = await access.AuthorizeAsync(input.CaseId, input.AssigneeUserId, "schedule.tasks.manage", SchedulingErrors.TaskNotFound, ct);
        if (allowed.IsFailure) { return allowed.Error; }
        return await access.AssigneeAsync(input.CaseId, input.AssigneeUserId, "schedule.tasks.read", ct);
    }

    public async Task<TaskDetailResponse> SaveAsync(TaskItem task, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        return TaskDetailResponse.Of(task);
    }
}

internal sealed class RegisterTaskHandler(ISchedulingDbContext db, TaskWrites writes, AgendaAccess access, ICurrentUser user, IClock clock)
    : ICommandHandler<RegisterTaskCommand, TaskDetailResponse>
{
    public async Task<Result<TaskDetailResponse>> Handle(RegisterTaskCommand request, CancellationToken ct)
    {
        await db.LockDeadlinesAsync(ct);
        var value = TaskItem.Register(request.Input, user.UserId ?? Guid.Empty, clock.UtcNow, request.ParentTaskId);
        if (value.IsFailure) { return value.Error; }
        var allowed = await writes.ValidateInputAsync(request.Input, ct);
        if (allowed.IsFailure) { return allowed.Error; }
        if (request.ParentTaskId is { } parentId)
        {
            var parentAccess = await access.TaskAsync(parentId, "schedule.tasks.manage", ct);
            if (parentAccess.IsFailure) { return parentAccess.Error; }
            var parent = await db.Tasks.SingleAsync(t => t.Id == parentId, ct);
            if (parent.ParentTaskId is not null || parent.IsClosed || parent.LegalCaseId != request.Input.CaseId) { return SchedulingErrors.InvalidSubtask; }
        }
        db.Tasks.Add(value.Value);
        return await writes.SaveAsync(value.Value, ct);
    }
}

internal sealed class GetTaskHandler(ISchedulingDbContext db) : IQueryHandler<GetTaskQuery, TaskDetailResponse>
{
    public async Task<Result<TaskDetailResponse>> Handle(GetTaskQuery query, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().Include(t => t.Checklist).Include(t => t.Comments).SingleOrDefaultAsync(t => t.Id == query.TaskId, ct);
        return task is null ? SchedulingErrors.TaskNotFound : TaskDetailResponse.Of(task);
    }
}

internal sealed class EditTaskHandler(TaskWrites writes) : ICommandHandler<EditTaskCommand, TaskDetailResponse>
{
    public async Task<Result<TaskDetailResponse>> Handle(EditTaskCommand command, CancellationToken ct)
    {
        var task = await writes.LoadAsync(command.TaskId, ct);
        if (task.IsFailure) { return task.Error; }
        var allowed = await writes.ValidateInputAsync(command.Input, ct);
        if (allowed.IsFailure) { return allowed.Error; }
        var result = task.Value.Edit(command.Input);
        return result.IsFailure ? result.Error : await writes.SaveAsync(task.Value, ct);
    }
}

internal sealed class ChangeTaskStateHandler(ISchedulingDbContext db, TaskWrites writes, ICurrentUser user, IClock clock)
    : ICommandHandler<ChangeTaskStateCommand, TaskDetailResponse>
{
    public async Task<Result<TaskDetailResponse>> Handle(ChangeTaskStateCommand command, CancellationToken ct)
    {
        var task = await writes.LoadAsync(command.TaskId, ct);
        if (task.IsFailure) { return task.Error; }
        var unresolved = await db.Tasks.AnyAsync(t => t.ParentTaskId == command.TaskId && t.Status != TaskState.Completed && t.Status != TaskState.Cancelled, ct);
        var result = task.Value.ChangeState(command.Status, user.UserId ?? Guid.Empty, clock.UtcNow, unresolved);
        return result.IsFailure ? result.Error : await writes.SaveAsync(task.Value, ct);
    }
}

internal sealed class AddChecklistHandler(TaskWrites writes) : ICommandHandler<AddChecklistCommand, TaskDetailResponse>
{
    public async Task<Result<TaskDetailResponse>> Handle(AddChecklistCommand command, CancellationToken ct)
    {
        var task = await writes.LoadAsync(command.TaskId, ct);
        if (task.IsFailure) { return task.Error; }
        var result = task.Value.AddChecklist(command.Title);
        return result.IsFailure ? result.Error : await writes.SaveAsync(task.Value, ct);
    }
}

internal sealed class SetChecklistHandler(TaskWrites writes) : ICommandHandler<SetChecklistCommand, TaskDetailResponse>
{
    public async Task<Result<TaskDetailResponse>> Handle(SetChecklistCommand command, CancellationToken ct)
    {
        var task = await writes.LoadAsync(command.TaskId, ct);
        if (task.IsFailure) { return task.Error; }
        var result = task.Value.SetChecklist(command.ItemId, command.Completed);
        return result.IsFailure ? result.Error : await writes.SaveAsync(task.Value, ct);
    }
}

internal sealed class AddTaskCommentHandler(TaskWrites writes, ICurrentUser user, IClock clock) : ICommandHandler<AddTaskCommentCommand, TaskDetailResponse>
{
    public async Task<Result<TaskDetailResponse>> Handle(AddTaskCommentCommand command, CancellationToken ct)
    {
        var task = await writes.LoadAsync(command.TaskId, ct);
        if (task.IsFailure) { return task.Error; }
        var result = task.Value.AddComment(command.Text, user.UserId ?? Guid.Empty, clock.UtcNow);
        return result.IsFailure ? result.Error : await writes.SaveAsync(task.Value, ct);
    }
}

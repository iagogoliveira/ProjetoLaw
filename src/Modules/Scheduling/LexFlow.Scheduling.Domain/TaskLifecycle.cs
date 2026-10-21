using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain;

public enum TaskState { Pending, InProgress, Completed, Cancelled }
public enum TaskPriority { Low, Normal, High, Urgent }
public sealed record TaskInput(Guid? CaseId, string Title, string? Description, DateOnly DueOn, Guid AssigneeUserId, TaskPriority Priority);

public sealed partial class TaskItem
{
    private readonly List<TaskChecklistItem> _checklist = [];
    private readonly List<TaskComment> _comments = [];
    public Guid? ParentTaskId { get; private set; }
    [Sensitive]
    public string? Description { get; private set; }
    public TaskPriority Priority { get; private set; } = TaskPriority.Normal;
    public IReadOnlyCollection<TaskChecklistItem> Checklist => _checklist.AsReadOnly();
    public IReadOnlyCollection<TaskComment> Comments => _comments.AsReadOnly();
    public Guid? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public bool IsClosed => Status is TaskState.Completed or TaskState.Cancelled;

    public static Result<TaskItem> Register(TaskInput input, Guid userId, DateTimeOffset now, Guid? parentTaskId = null)
    {
        if (!Valid(input) || userId == Guid.Empty || parentTaskId == Guid.Empty) { return SchedulingErrors.InvalidTask; }
        return new TaskItem
        {
            Id = Guid.CreateVersion7(),
            LegalCaseId = input.CaseId,
            Title = input.Title.Trim(),
            Description = input.Description?.Trim(),
            DueOn = input.DueOn,
            AssigneeUserId = input.AssigneeUserId,
            Priority = input.Priority,
            ParentTaskId = parentTaskId,
            ConfirmedBy = userId,
            ConfirmedAt = now.ToUniversalTime(),
        };
    }

    public Result Edit(TaskInput input)
    {
        if (IsClosed) { return SchedulingErrors.TaskTransitionInvalid; }
        if (!Valid(input) || input.CaseId != LegalCaseId) { return SchedulingErrors.InvalidTask; }
        Title = input.Title.Trim(); Description = input.Description?.Trim(); DueOn = input.DueOn;
        AssigneeUserId = input.AssigneeUserId; Priority = input.Priority;
        return Result.Success;
    }

    public Result ChangeState(TaskState target, Guid userId, DateTimeOffset now, bool hasUnresolvedChildren)
    {
        if (!Enum.IsDefined(target) || userId == Guid.Empty || target == Status
            || (IsClosed && target != TaskState.Pending) || (!IsClosed && target == TaskState.Pending && Status != TaskState.InProgress))
        { return SchedulingErrors.TaskTransitionInvalid; }
        if (target == TaskState.Completed && (hasUnresolvedChildren || _checklist.Any(i => !i.Completed)))
        { return SchedulingErrors.TaskUnresolved; }
        Status = target;
        ClosedBy = IsClosed ? userId : null;
        ClosedAt = IsClosed ? now.ToUniversalTime() : null;
        return Result.Success;
    }

    public Result Reassign(Guid assignee)
    {
        if (IsClosed) { return SchedulingErrors.TaskTransitionInvalid; }
        if (assignee == Guid.Empty) { return SchedulingErrors.AssigneeUnavailable; }
        AssigneeUserId = assignee;
        return Result.Success;
    }

    public Result<TaskChecklistItem> AddChecklist(string title)
    {
        if (IsClosed) { return SchedulingErrors.TaskTransitionInvalid; }
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 300 || _checklist.Count >= 100) { return SchedulingErrors.InvalidChecklist; }
        var item = TaskChecklistItem.Create(this, title.Trim());
        _checklist.Add(item);
        return item;
    }

    public Result SetChecklist(Guid itemId, bool completed)
    {
        if (IsClosed) { return SchedulingErrors.TaskTransitionInvalid; }
        var item = _checklist.Find(i => i.Id == itemId);
        if (item is null) { return SchedulingErrors.ChecklistNotFound; }
        item.SetCompleted(completed);
        return Result.Success;
    }

    public Result<TaskComment> AddComment(string text, Guid userId, DateTimeOffset now)
    {
        if (IsClosed) { return SchedulingErrors.TaskTransitionInvalid; }
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 4000 || userId == Guid.Empty) { return SchedulingErrors.InvalidComment; }
        var comment = TaskComment.Create(this, text.Trim(), userId, now);
        _comments.Add(comment);
        return comment;
    }

    private static bool Valid(TaskInput input) => input.CaseId != Guid.Empty && !string.IsNullOrWhiteSpace(input.Title)
        && input.Title.Trim().Length <= 300 && (input.Description?.Length ?? 0) <= 4000 && input.DueOn != default
        && input.AssigneeUserId != Guid.Empty && Enum.IsDefined(input.Priority);
}

public sealed class TaskChecklistItem : Entity<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    private TaskChecklistItem() { }
    public Guid OrganizationId { get; private set; }
    public Guid? LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public Guid TaskId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public bool Completed { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    internal static TaskChecklistItem Create(TaskItem task, string title) => new()
    { Id = Guid.CreateVersion7(), OrganizationId = task.OrganizationId, LegalCaseId = task.LegalCaseId, TaskId = task.Id, Title = title };
    internal void SetCompleted(bool completed) => Completed = completed;
}

public sealed class TaskComment : Entity<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    private TaskComment() { }
    public Guid OrganizationId { get; private set; }
    public Guid? LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public Guid TaskId { get; private set; }
    [Sensitive]
    public string Text { get; private set; } = string.Empty;
    public Guid AuthorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    internal static TaskComment Create(TaskItem task, string text, Guid userId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        OrganizationId = task.OrganizationId,
        LegalCaseId = task.LegalCaseId,
        TaskId = task.Id,
        Text = text,
        AuthorUserId = userId,
        OccurredAt = now.ToUniversalTime()
    };
}

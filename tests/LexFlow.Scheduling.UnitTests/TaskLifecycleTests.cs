using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.SharedKernel.Domain;

namespace LexFlow.Scheduling.UnitTests;

public sealed class TaskLifecycleTests
{
    private static readonly DateOnly DueOn = new(2026, 11, 2);
    private static readonly DateTimeOffset LocalNow = new(2026, 10, 20, 10, 15, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset UtcNow = LocalNow.ToUniversalTime();
    private readonly Guid _caseId = Guid.CreateVersion7();
    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _otherUserId = Guid.CreateVersion7();

    [Fact]
    public void Register_allows_an_optional_case_and_publication_and_normalizes_values()
    {
        var task = TaskItem.Register(Input(withoutCase: true, title: "  Review file  ", description: "  Check facts  "), _userId, LocalNow).Value;

        task.Id.ShouldNotBe(Guid.Empty);
        task.LegalCaseId.ShouldBeNull();
        task.PublicationId.ShouldBeNull();
        task.Title.ShouldBe("Review file");
        task.Description.ShouldBe("Check facts");
        task.DueOn.ShouldBe(DueOn);
        task.AssigneeUserId.ShouldBe(_otherUserId);
        task.Priority.ShouldBe(TaskPriority.High);
        task.Status.ShouldBe(TaskState.Pending);
        task.ConfirmedBy.ShouldBe(_userId);
        task.ConfirmedAt.ShouldBe(UtcNow);
        task.ConfirmedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Register_accepts_maximum_text_lengths_and_every_defined_priority()
    {
        foreach (var priority in Enum.GetValues<TaskPriority>())
        {
            var result = TaskItem.Register(Input(title: new string('t', 300), description: new string('d', 4000), priority: priority), _userId, LocalNow);

            result.IsSuccess.ShouldBeTrue();
            result.Value.Priority.ShouldBe(priority);
        }
    }

    [Fact]
    public void Register_rejects_missing_required_values_and_out_of_range_fields()
    {
        TaskItem.Register(Input(caseId: Guid.Empty), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidTask);
        TaskItem.Register(Input(title: "  "), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidTask);
        TaskItem.Register(Input(title: new string('t', 301)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidTask);
        TaskItem.Register(Input(description: new string('d', 4001)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidTask);
        TaskItem.Register(Input(dueOn: DateOnly.MinValue), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidTask);
        TaskItem.Register(Input(assignee: Guid.Empty), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidTask);
        TaskItem.Register(Input(priority: (TaskPriority)99), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidTask);
        TaskItem.Register(Input(), Guid.Empty, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidTask);
        TaskItem.Register(Input(), _userId, LocalNow, Guid.Empty).Error.ShouldBe(SchedulingErrors.InvalidTask);
    }

    [Fact]
    public void Task_can_start_return_to_pending_and_reopen_terminal_states()
    {
        var task = CreateTask();

        task.ChangeState(TaskState.InProgress, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.Status.ShouldBe(TaskState.InProgress);
        task.ClosedBy.ShouldBeNull();
        task.ClosedAt.ShouldBeNull();

        task.ChangeState(TaskState.Pending, _userId, LocalNow.AddMinutes(1), hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.Status.ShouldBe(TaskState.Pending);

        task.ChangeState(TaskState.Completed, _otherUserId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.ClosedBy.ShouldBe(_otherUserId);
        task.ClosedAt.ShouldBe(UtcNow);
        task.ClosedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);

        task.ChangeState(TaskState.Pending, _userId, LocalNow.AddHours(1), hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.Status.ShouldBe(TaskState.Pending);
        task.ClosedBy.ShouldBeNull();
        task.ClosedAt.ShouldBeNull();

        task.ChangeState(TaskState.Cancelled, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.ChangeState(TaskState.Pending, _userId, LocalNow.AddHours(2), hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.Status.ShouldBe(TaskState.Pending);
        task.ClosedBy.ShouldBeNull();
        task.ClosedAt.ShouldBeNull();
    }

    [Fact]
    public void Invalid_state_transitions_and_empty_actor_are_rejected_without_changing_state()
    {
        var task = CreateTask();

        task.ChangeState(TaskState.Pending, _userId, LocalNow, hasUnresolvedChildren: false).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
        task.ChangeState(TaskState.InProgress, Guid.Empty, LocalNow, hasUnresolvedChildren: false).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
        task.ChangeState((TaskState)99, _userId, LocalNow, hasUnresolvedChildren: false).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
        task.Status.ShouldBe(TaskState.Pending);

        task.ChangeState(TaskState.InProgress, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.ChangeState(TaskState.InProgress, _userId, LocalNow, hasUnresolvedChildren: false).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
        task.Status.ShouldBe(TaskState.InProgress);
    }

    [Fact]
    public void Open_tasks_can_be_cancelled_from_pending_or_in_progress()
    {
        var pending = CreateTask();
        pending.ChangeState(TaskState.Cancelled, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        pending.Status.ShouldBe(TaskState.Cancelled);

        var inProgress = CreateTask();
        inProgress.ChangeState(TaskState.InProgress, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        inProgress.ChangeState(TaskState.Cancelled, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        inProgress.Status.ShouldBe(TaskState.Cancelled);
    }

    [Theory]
    [InlineData(TaskState.Completed, TaskState.InProgress)]
    [InlineData(TaskState.Completed, TaskState.Cancelled)]
    [InlineData(TaskState.Cancelled, TaskState.InProgress)]
    [InlineData(TaskState.Cancelled, TaskState.Completed)]
    public void Terminal_tasks_cannot_transition_to_another_terminal_or_open_state(TaskState terminal, TaskState target)
    {
        var task = CreateTask();
        task.ChangeState(terminal, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        var closedAt = task.ClosedAt;

        task.ChangeState(target, _otherUserId, LocalNow.AddHours(1), hasUnresolvedChildren: false)
            .Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);

        task.Status.ShouldBe(terminal);
        task.ClosedAt.ShouldBe(closedAt);
    }

    [Fact]
    public void Completion_requires_a_complete_checklist_and_resolved_subtasks()
    {
        var task = CreateTask();
        var checklist = task.AddChecklist("File the signed copy").Value;

        task.ChangeState(TaskState.Completed, _userId, LocalNow, hasUnresolvedChildren: false).Error.ShouldBe(SchedulingErrors.TaskUnresolved);
        task.ChangeState(TaskState.Completed, _userId, LocalNow, hasUnresolvedChildren: true).Error.ShouldBe(SchedulingErrors.TaskUnresolved);
        task.Status.ShouldBe(TaskState.Pending);

        task.SetChecklist(checklist.Id, completed: true).IsSuccess.ShouldBeTrue();
        task.ChangeState(TaskState.Completed, _userId, LocalNow, hasUnresolvedChildren: true).Error.ShouldBe(SchedulingErrors.TaskUnresolved);
        task.ChangeState(TaskState.Completed, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.Status.ShouldBe(TaskState.Completed);
    }

    [Fact]
    public void Registering_a_subtask_preserves_parent_case_and_parent_identity()
    {
        var parent = CreateTask();

        var child = TaskItem.Register(Input(caseId: _caseId, title: "Collect exhibit"), _otherUserId, LocalNow, parent.Id).Value;

        child.ParentTaskId.ShouldBe(parent.Id);
        child.LegalCaseId.ShouldBe(parent.LegalCaseId);
        ((ICaseScoped)child).CaseId.ShouldBe(_caseId);
        child.Status.ShouldBe(TaskState.Pending);
    }

    [Fact]
    public void Checklist_enforces_limits_ids_and_closed_task_guard()
    {
        var task = CreateTask();
        task.AddChecklist("  Verify citation  ").Value.Title.ShouldBe("Verify citation");
        task.AddChecklist(new string('c', 300)).IsSuccess.ShouldBeTrue();
        task.AddChecklist(" ").Error.ShouldBe(SchedulingErrors.InvalidChecklist);
        task.AddChecklist(new string('c', 301)).Error.ShouldBe(SchedulingErrors.InvalidChecklist);
        for (var index = 2; index < 100; index++)
        {
            task.AddChecklist($"Item {index}").IsSuccess.ShouldBeTrue();
        }
        task.AddChecklist("101st item").Error.ShouldBe(SchedulingErrors.InvalidChecklist);
        task.SetChecklist(Guid.CreateVersion7(), true).Error.ShouldBe(SchedulingErrors.ChecklistNotFound);
        task.SetChecklist(task.Checklist.First().Id, true).IsSuccess.ShouldBeTrue();

        task.ChangeState(TaskState.Cancelled, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.AddChecklist("closed").Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
        task.SetChecklist(task.Checklist.First().Id, false).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
    }

    [Fact]
    public void Comments_keep_task_case_author_and_utc_timestamp_and_validate_limits()
    {
        var task = CreateTask();
        task.AddComment(" ", _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidComment);
        task.AddComment(new string('c', 4001), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidComment);
        task.AddComment("comment", Guid.Empty, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidComment);

        var comment = task.AddComment("  Discussed with client  ", _otherUserId, LocalNow).Value;

        comment.Id.ShouldNotBe(Guid.Empty);
        comment.TaskId.ShouldBe(task.Id);
        comment.LegalCaseId.ShouldBe(_caseId);
        ((ICaseScoped)comment).CaseId.ShouldBe(_caseId);
        comment.Text.ShouldBe("Discussed with client");
        comment.AuthorUserId.ShouldBe(_otherUserId);
        comment.OccurredAt.ShouldBe(UtcNow);
        comment.OccurredAt.Offset.ShouldBe(TimeSpan.Zero);
        task.AddComment(new string('c', 4000), _userId, LocalNow).Value.Text.Length.ShouldBe(4000);

        task.ChangeState(TaskState.Completed, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();
        task.AddComment("closed", _userId, LocalNow).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
    }

    [Fact]
    public void Closed_tasks_cannot_be_edited_or_reassigned()
    {
        var task = CreateTask();
        task.ChangeState(TaskState.Cancelled, _userId, LocalNow, hasUnresolvedChildren: false).IsSuccess.ShouldBeTrue();

        task.Edit(Input(title: "Changed")).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
        task.Reassign(_otherUserId).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
        task.Reassign(Guid.Empty).Error.ShouldBe(SchedulingErrors.TaskTransitionInvalid);
    }

    [Fact]
    public void Reassignment_requires_a_responsible_for_open_tasks()
    {
        var task = CreateTask();

        task.Reassign(Guid.Empty).Error.ShouldBe(SchedulingErrors.AssigneeUnavailable);
        task.Reassign(_otherUserId).IsSuccess.ShouldBeTrue();
        task.AssigneeUserId.ShouldBe(_otherUserId);
    }

    [Fact]
    public void Deadlines_can_be_reassigned_while_pending_or_in_progress_without_changing_dates_or_review()
    {
        var pending = CreateDeadline(requireReview: true);
        var pendingDates = (pending.StartOn, pending.InternalDueOn, pending.FatalDueOn);

        pending.Reassign(_otherUserId).IsSuccess.ShouldBeTrue();

        pending.AssigneeUserId.ShouldBe(_otherUserId);
        (pending.StartOn, pending.InternalDueOn, pending.FatalDueOn).ShouldBe(pendingDates);
        pending.RequireReview.ShouldBeTrue();
        pending.ReviewedBy.ShouldBeNull();
        pending.ReviewedAt.ShouldBeNull();

        var inProgress = CreateDeadline(requireReview: true);
        inProgress.Start().IsSuccess.ShouldBeTrue();
        inProgress.Review(_otherUserId, singleMember: false, LocalNow).IsSuccess.ShouldBeTrue();
        var inProgressDates = (inProgress.StartOn, inProgress.InternalDueOn, inProgress.FatalDueOn);
        var reviewer = inProgress.ReviewedBy;
        var reviewedAt = inProgress.ReviewedAt;

        inProgress.Reassign(_userId).IsSuccess.ShouldBeTrue();

        inProgress.AssigneeUserId.ShouldBe(_userId);
        (inProgress.StartOn, inProgress.InternalDueOn, inProgress.FatalDueOn).ShouldBe(inProgressDates);
        inProgress.RequireReview.ShouldBeTrue();
        inProgress.ReviewedBy.ShouldBe(reviewer);
        inProgress.ReviewedAt.ShouldBe(reviewedAt);
    }

    [Theory]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Missed)]
    public void Closed_deadlines_cannot_be_reassigned(WorkItemStatus terminalStatus)
    {
        var deadline = CreateDeadline();
        if (terminalStatus == WorkItemStatus.Completed)
        {
            deadline.Complete("protocol", _userId, LocalNow).IsSuccess.ShouldBeTrue();
        }
        else
        {
            deadline.MarkMissed("reason", _userId, LocalNow).IsSuccess.ShouldBeTrue();
        }

        deadline.Reassign(_otherUserId).Error.ShouldBe(SchedulingErrors.InvalidTransition);
        deadline.Status.ShouldBe(terminalStatus);
    }

    [Fact]
    public void Open_deadline_reassignment_requires_a_nonempty_responsible()
    {
        var deadline = CreateDeadline();

        deadline.Reassign(Guid.Empty).Error.ShouldBe(SchedulingErrors.AssigneeUnavailable);
        deadline.Status.ShouldBe(WorkItemStatus.Pending);
    }

    private TaskItem CreateTask() => TaskItem.Register(Input(), _userId, LocalNow).Value;

    private Deadline CreateDeadline(bool requireReview = false) => Deadline.CreateManual(new ManualDeadlineInput(
        _caseId, "appeal", "Submit appeal", _otherUserId,
        new DeadlineRule(DeadlineStartKind.EventDate, DueOn, null, 10, CountingMode.BusinessDays, false, 4),
        DueOn, DueOn.AddDays(4), DueOn.AddDays(10), "reviewed calculation", null, "SP", "São Paulo", requireReview, true),
        _userId, LocalNow).Value;

    private TaskInput Input(Guid? caseId = null, string title = "Analyze case", string? description = null,
        DateOnly? dueOn = null, Guid? assignee = null, TaskPriority priority = TaskPriority.High, bool withoutCase = false) =>
        new(withoutCase ? null : caseId ?? _caseId, title, description, dueOn ?? DueOn, assignee ?? _otherUserId, priority);
}

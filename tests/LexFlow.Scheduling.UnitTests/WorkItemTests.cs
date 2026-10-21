using LexFlow.Scheduling.Domain;

namespace LexFlow.Scheduling.UnitTests;

public sealed class WorkItemTests
{
    private static readonly DateOnly Start = new(2026, 10, 19);
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.FromHours(-3));
    private readonly Guid _case = Guid.CreateVersion7();
    private readonly Guid _publication = Guid.CreateVersion7();
    private readonly Guid _user = Guid.CreateVersion7();

    [Fact]
    public void Human_confirmed_deadline_keeps_dates_and_confirmation_in_utc()
    {
        var deadline = Deadline.Create(_case, _publication, "  Contestação  ", Start, Start.AddDays(10), Start.AddDays(15), _user, true, _user, Now).Value;

        deadline.Id.ShouldNotBe(Guid.Empty);
        deadline.LegalCaseId.ShouldBe(_case);
        deadline.PublicationId.ShouldBe(_publication);
        deadline.Title.ShouldBe("Contestação");
        deadline.StartOn.ShouldBe(Start);
        deadline.InternalDueOn.ShouldBe(Start.AddDays(10));
        deadline.FatalDueOn.ShouldBe(Start.AddDays(15));
        deadline.AssigneeUserId.ShouldBe(_user);
        deadline.ConfirmedBy.ShouldBe(_user);
        deadline.ConfirmedAt.Offset.ShouldBe(TimeSpan.Zero);
        deadline.ConfirmedAt.ShouldBe(Now);
        deadline.Status.ShouldBe(WorkItemStatus.Pending);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(3, 2)]
    public void Deadline_rejects_internal_date_before_start_or_after_fatal(int internalOffset, int fatalOffset) =>
        Deadline.Create(_case, _publication, "Prazo", Start, Start.AddDays(internalOffset), Start.AddDays(fatalOffset), _user, true, _user, Now)
            .Error.ShouldBe(SchedulingErrors.InvalidDeadline);

    [Fact]
    public void Deadline_requires_explicit_start_date() =>
        Deadline.Create(_case, _publication, "Prazo", default, Start, Start, _user, true, _user, Now).Error.ShouldBe(SchedulingErrors.InvalidDeadline);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Both_items_require_identifiers_title_responsible_and_human_confirmation(int invalid)
    {
        var caseId = invalid == 0 ? Guid.Empty : _case;
        var publication = invalid == 1 ? Guid.Empty : _publication;
        var title = invalid == 2 ? "  " : invalid == 3 ? new string('x', 301) : "Título";
        var assignee = invalid == 4 ? Guid.Empty : _user;
        var confirmed = invalid != 5;
        var user = invalid == 6 ? Guid.Empty : _user;

        Deadline.Create(caseId, publication, title, Start, Start, Start, assignee, confirmed, user, Now).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        TaskItem.Create(caseId, publication, title, Start, assignee, confirmed, user, Now).Error.ShouldBe(SchedulingErrors.InvalidTask);
    }

    [Fact]
    public void Task_requires_due_date() =>
        TaskItem.Create(_case, _publication, "Tarefa", default, _user, true, _user, Now).Error.ShouldBe(SchedulingErrors.InvalidTask);

    [Fact]
    public void Confirmed_task_is_pending_and_keeps_responsible_and_origin()
    {
        var task = TaskItem.Create(_case, _publication, "  Analisar decisão  ", Start, _user, true, _user, Now).Value;

        task.Id.ShouldNotBe(Guid.Empty);
        task.LegalCaseId.ShouldBe(_case);
        task.PublicationId.ShouldBe(_publication);
        task.Title.ShouldBe("Analisar decisão");
        task.DueOn.ShouldBe(Start);
        task.AssigneeUserId.ShouldBe(_user);
        task.ConfirmedBy.ShouldBe(_user);
        task.ConfirmedAt.Offset.ShouldBe(TimeSpan.Zero);
        task.Status.ShouldBe(TaskState.Pending);
    }
}

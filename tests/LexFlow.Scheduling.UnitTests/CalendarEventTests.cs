using LexFlow.Scheduling.Domain;

namespace LexFlow.Scheduling.UnitTests;

public sealed class CalendarEventTests
{
    private static readonly DateTimeOffset LocalStart = new(2026, 10, 21, 9, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset LocalEnd = new(2026, 10, 21, 10, 0, 0, TimeSpan.FromHours(-3));
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid OtherUserId = Guid.CreateVersion7();
    private readonly Guid _caseId = Guid.CreateVersion7();

    [Fact]
    public void Create_allows_optional_case_and_normalizes_text_and_times()
    {
        var calendarEvent = CalendarEvent.Create(Input(withoutCase: true, title: "  Client meeting  ", description: "  Prepare questions  ", location: "  Room 2  ")).Value;

        calendarEvent.Id.ShouldNotBe(Guid.Empty);
        calendarEvent.LegalCaseId.ShouldBeNull();
        calendarEvent.Title.ShouldBe("Client meeting");
        calendarEvent.Description.ShouldBe("Prepare questions");
        calendarEvent.Location.ShouldBe("Room 2");
        calendarEvent.StartsAt.ShouldBe(LocalStart.ToUniversalTime());
        calendarEvent.EndsAt.ShouldBe(LocalEnd.ToUniversalTime());
        calendarEvent.StartsAt.Offset.ShouldBe(TimeSpan.Zero);
        calendarEvent.EndsAt.Offset.ShouldBe(TimeSpan.Zero);
        calendarEvent.AssigneeUserId.ShouldBe(UserId);
        calendarEvent.Status.ShouldBe(CalendarEventState.Scheduled);
    }

    [Fact]
    public void Create_accepts_maximum_field_lengths()
    {
        var result = CalendarEvent.Create(Input(title: new string('t', 300), description: new string('d', 4000), location: new string('l', 500)));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.Length.ShouldBe(300);
        result.Value.Description!.Length.ShouldBe(4000);
        result.Value.Location!.Length.ShouldBe(500);
    }

    [Fact]
    public void Create_rejects_invalid_case_title_times_responsible_and_text_lengths()
    {
        CalendarEvent.Create(Input(caseId: Guid.Empty)).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(title: " ")).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(title: new string('t', 301))).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(description: new string('d', 4001))).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(location: new string('l', 501))).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(startsAt: DateTimeOffset.MinValue)).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(endsAt: DateTimeOffset.MinValue)).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(endsAt: LocalStart)).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(endsAt: LocalStart.AddMinutes(-1))).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        CalendarEvent.Create(Input(assignee: Guid.Empty)).Error.ShouldBe(SchedulingErrors.InvalidEvent);
    }

    [Fact]
    public void Edit_updates_an_open_event_but_cannot_change_case()
    {
        var calendarEvent = CalendarEvent.Create(Input()).Value;
        var updated = CalendarEvent.Create(Input(caseId: _caseId, title: "Updated", startsAt: LocalStart.AddDays(1), endsAt: LocalEnd.AddDays(1), assignee: OtherUserId)).Value;

        calendarEvent.Edit(Input(caseId: Guid.CreateVersion7())).Error.ShouldBe(SchedulingErrors.InvalidEvent);
        calendarEvent.Edit(Input(caseId: _caseId, title: "  Updated  ", startsAt: LocalStart.AddDays(1), endsAt: LocalEnd.AddDays(1), assignee: OtherUserId)).IsSuccess.ShouldBeTrue();

        calendarEvent.Title.ShouldBe("Updated");
        calendarEvent.StartsAt.ShouldBe(updated.StartsAt);
        calendarEvent.EndsAt.ShouldBe(updated.EndsAt);
        calendarEvent.AssigneeUserId.ShouldBe(OtherUserId);
    }

    [Fact]
    public void Cancel_and_reassign_are_only_allowed_while_scheduled()
    {
        var calendarEvent = CalendarEvent.Create(Input()).Value;

        calendarEvent.Reassign(Guid.Empty).Error.ShouldBe(SchedulingErrors.AssigneeUnavailable);
        calendarEvent.Reassign(OtherUserId).IsSuccess.ShouldBeTrue();
        calendarEvent.Cancel().IsSuccess.ShouldBeTrue();
        calendarEvent.Status.ShouldBe(CalendarEventState.Cancelled);
        calendarEvent.Cancel().Error.ShouldBe(SchedulingErrors.EventClosed);
        calendarEvent.Reassign(UserId).Error.ShouldBe(SchedulingErrors.EventClosed);
        calendarEvent.Edit(Input(title: "After cancellation")).Error.ShouldBe(SchedulingErrors.EventClosed);
    }

    private CalendarEventInput Input(Guid? caseId = null, string title = "Hearing preparation", string? description = null,
        DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null, Guid? assignee = null, string? location = null, bool withoutCase = false) =>
        new(withoutCase ? null : caseId ?? _caseId, title, description, startsAt ?? LocalStart, endsAt ?? LocalEnd, assignee ?? UserId, location);
}

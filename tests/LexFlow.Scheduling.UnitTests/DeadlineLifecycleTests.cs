using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.SharedKernel.Domain;

namespace LexFlow.Scheduling.UnitTests;

public sealed class DeadlineLifecycleTests
{
    private static readonly DateOnly StartOn = new(2026, 10, 20);
    private static readonly DateTimeOffset LocalNow = new(2026, 10, 20, 10, 15, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset UtcNow = LocalNow.ToUniversalTime();
    private readonly Guid _caseId = Guid.CreateVersion7();
    private readonly Guid _userId = Guid.CreateVersion7();
    private readonly Guid _otherUserId = Guid.CreateVersion7();

    [Fact]
    public void Manual_deadline_keeps_rule_snapshot_location_and_human_dates_without_publication_origin()
    {
        var rule = ValidRule();
        var input = ValidInput(rule: rule, confirmed: true, requireReview: true);

        var deadline = Deadline.CreateManual(input, _userId, LocalNow).Value;

        deadline.Id.ShouldNotBe(Guid.Empty);
        deadline.LegalCaseId.ShouldBe(_caseId);
        deadline.PublicationId.ShouldBeNull();
        deadline.Type.ShouldBe("appeal");
        deadline.Title.ShouldBe("Submit appeal");
        deadline.AssigneeUserId.ShouldBe(_otherUserId);
        deadline.Rule.ShouldBe(rule);
        deadline.CalculationSnapshot.ShouldBe("calculation reviewed by the user");
        deadline.CourtId.ShouldBe(input.CourtId);
        deadline.StateCode.ShouldBe("SP");
        deadline.Municipality.ShouldBe("São Paulo");
        deadline.StartOn.ShouldBe(StartOn);
        deadline.InternalDueOn.ShouldBe(StartOn.AddDays(4));
        deadline.FatalDueOn.ShouldBe(StartOn.AddDays(10));
        deadline.ConfirmedBy.ShouldBe(_userId);
        deadline.ConfirmedAt.ShouldBe(UtcNow);
        deadline.ConfirmedAt.Offset.ShouldBe(TimeSpan.Zero);
        deadline.RequireReview.ShouldBeTrue();
        deadline.Status.ShouldBe(WorkItemStatus.Pending);
    }

    [Fact]
    public void Manual_deadline_requires_explicit_human_confirmation() =>
        Deadline.CreateManual(ValidInput(confirmed: false), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);

    [Theory]
    [InlineData(" ", "appeal")]
    [InlineData("Submit appeal", " ")]
    public void Manual_deadline_rejects_blank_title_or_type(string title, string type) =>
        Deadline.CreateManual(ValidInput(title: title, type: type), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);

    [Fact]
    public void Manual_deadline_rejects_title_and_type_over_their_limits()
    {
        Deadline.CreateManual(ValidInput(title: new string('t', 301)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput(type: new string('t', 81)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
    }

    [Fact]
    public void Manual_deadline_rejects_missing_snapshot_and_invalid_rule()
    {
        Deadline.CreateManual(ValidInput(calculationSnapshot: " "), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput() with { Rule = null! }, _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput(rule: ValidRule(days: 0)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput(rule: ValidRule(internalLeadDays: -1)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput(rule: ValidRule() with { TriggerDate = default }), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput(rule: ValidRule(startKind: (DeadlineStartKind)99)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput(rule: ValidRule(mode: (CountingMode)99)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
    }

    [Fact]
    public void Manual_deadline_rejects_default_or_out_of_order_dates()
    {
        Deadline.CreateManual(ValidInput() with { StartOn = default }, _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput(internalDueOn: StartOn.AddDays(-1)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
        Deadline.CreateManual(ValidInput(fatalDueOn: StartOn.AddDays(3)), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.InvalidDeadline);
    }

    [Fact]
    public void Starting_a_deadline_does_not_require_review_even_when_review_is_required_for_completion()
    {
        var deadline = CreateDeadline(requireReview: true);

        var result = deadline.Start();

        result.IsSuccess.ShouldBeTrue();
        deadline.Status.ShouldBe(WorkItemStatus.InProgress);
        deadline.ReviewedBy.ShouldBeNull();
    }

    [Fact]
    public void Starting_an_already_started_deadline_is_rejected()
    {
        var deadline = CreateDeadline();

        deadline.Start().IsSuccess.ShouldBeTrue();
        deadline.Start().Error.ShouldBe(SchedulingErrors.InvalidTransition);
        deadline.Status.ShouldBe(WorkItemStatus.InProgress);
    }

    [Fact]
    public void Completing_requires_review_only_when_the_deadline_requires_it()
    {
        var reviewRequired = CreateDeadline(requireReview: true);
        reviewRequired.Start().IsSuccess.ShouldBeTrue();
        reviewRequired.Complete("protocol 123", _userId, LocalNow).Error.ShouldBe(SchedulingErrors.ReviewRequired);
        reviewRequired.Status.ShouldBe(WorkItemStatus.InProgress);

        var reviewOptional = CreateDeadline(requireReview: false);
        reviewOptional.Start().IsSuccess.ShouldBeTrue();
        reviewOptional.Complete("  protocol 456  ", _userId, LocalNow).IsSuccess.ShouldBeTrue();
        reviewOptional.CompletionEvidence.ShouldBe("protocol 456");
        reviewOptional.Status.ShouldBe(WorkItemStatus.Completed);
        reviewOptional.ClosedBy.ShouldBe(_userId);
        reviewOptional.ClosedAt.ShouldBe(UtcNow);
        reviewOptional.ClosedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Completion_requires_trimmed_nonblank_evidence_of_at_most_4000_characters()
    {
        var deadline = CreateDeadline();
        deadline.Start().IsSuccess.ShouldBeTrue();

        deadline.Complete("  ", _userId, LocalNow).Error.ShouldBe(SchedulingErrors.EvidenceRequired);
        deadline.Complete(new string('e', 4001), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.EvidenceRequired);
        deadline.Complete(new string('e', 4000), _userId, LocalNow).IsSuccess.ShouldBeTrue();
        deadline.CompletionEvidence!.Length.ShouldBe(4000);
    }

    [Fact]
    public void Marking_a_deadline_missed_requires_trimmed_justification_of_at_most_4000_characters()
    {
        var deadline = CreateDeadline();
        deadline.Start().IsSuccess.ShouldBeTrue();

        deadline.MarkMissed("  ", _userId, LocalNow).Error.ShouldBe(SchedulingErrors.MissedReasonRequired);
        deadline.MarkMissed(new string('r', 4001), _userId, LocalNow).Error.ShouldBe(SchedulingErrors.MissedReasonRequired);
        deadline.MarkMissed("  Filing was not accepted  ", _userId, LocalNow).IsSuccess.ShouldBeTrue();
        deadline.MissedReason.ShouldBe("Filing was not accepted");
        deadline.Status.ShouldBe(WorkItemStatus.Missed);
        deadline.ClosedBy.ShouldBe(_userId);
        deadline.ClosedAt.ShouldBe(UtcNow);
        deadline.ClosedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(WorkItemStatus.Completed)]
    [InlineData(WorkItemStatus.Missed)]
    public void Terminal_deadline_cannot_be_started_completed_missed_or_reviewed(WorkItemStatus terminalStatus)
    {
        var deadline = CreateDeadline(requireReview: false);
        deadline.Start().IsSuccess.ShouldBeTrue();
        if (terminalStatus == WorkItemStatus.Completed)
        {
            deadline.Complete("protocol", _userId, LocalNow).IsSuccess.ShouldBeTrue();
        }
        else
        {
            deadline.MarkMissed("reason", _userId, LocalNow).IsSuccess.ShouldBeTrue();
        }
        var closedAt = deadline.ClosedAt;

        deadline.Start().Error.ShouldBe(SchedulingErrors.InvalidTransition);
        deadline.Complete("another protocol", _userId, LocalNow.AddHours(1)).Error.ShouldBe(SchedulingErrors.InvalidTransition);
        deadline.MarkMissed("another reason", _userId, LocalNow.AddHours(1)).Error.ShouldBe(SchedulingErrors.InvalidTransition);
        deadline.Review(_otherUserId, singleMember: false, LocalNow.AddHours(1)).Error.ShouldBe(SchedulingErrors.InvalidTransition);

        deadline.Status.ShouldBe(terminalStatus);
        deadline.ClosedAt.ShouldBe(closedAt);
    }

    [Fact]
    public void Review_requires_another_member_except_when_the_office_has_one_active_member()
    {
        var deadline = CreateDeadline();

        deadline.Review(_userId, singleMember: false, LocalNow).Error.ShouldBe(SchedulingErrors.SeparateReviewerRequired);
        deadline.Review(_otherUserId, singleMember: false, LocalNow).IsSuccess.ShouldBeTrue();
        deadline.ReviewedBy.ShouldBe(_otherUserId);
        deadline.ReviewedAt.ShouldBe(UtcNow);
        deadline.ReviewedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);

        var soloDeadline = CreateDeadline();
        soloDeadline.Review(_userId, singleMember: true, LocalNow).IsSuccess.ShouldBeTrue();
        soloDeadline.ReviewedBy.ShouldBe(_userId);
    }

    [Fact]
    public void Review_rejects_empty_reviewer_and_cannot_be_repeated()
    {
        var deadline = CreateDeadline();

        deadline.Review(Guid.Empty, singleMember: true, LocalNow).Error.ShouldBe(SchedulingErrors.SeparateReviewerRequired);
        deadline.Review(_otherUserId, singleMember: false, LocalNow).IsSuccess.ShouldBeTrue();
        deadline.Review(_userId, singleMember: false, LocalNow.AddMinutes(1)).Error.ShouldBe(SchedulingErrors.InvalidTransition);
        deadline.ReviewedBy.ShouldBe(_otherUserId);
        deadline.ReviewedAt.ShouldBe(UtcNow);
    }

    [Fact]
    public void Legacy_publication_deadline_still_requires_confirmation_and_keeps_its_origin()
    {
        var publicationId = Guid.CreateVersion7();
        var confirmed = Deadline.Create(_caseId, publicationId, "  Respond to notice  ", StartOn,
            StartOn.AddDays(4), StartOn.AddDays(10), _otherUserId, confirmed: true, _userId, LocalNow).Value;

        confirmed.PublicationId.ShouldBe(publicationId);
        confirmed.ConfirmedBy.ShouldBe(_userId);
        confirmed.ConfirmedAt.ShouldBe(UtcNow);
        confirmed.Status.ShouldBe(WorkItemStatus.Pending);

        Deadline.Create(_caseId, publicationId, "Respond to notice", StartOn,
            StartOn.AddDays(4), StartOn.AddDays(10), _otherUserId, confirmed: false, _userId, LocalNow)
            .Error.ShouldBe(SchedulingErrors.InvalidDeadline);
    }

    [Fact]
    public void Deadline_activity_records_case_actor_kind_and_calendar_reference_in_utc_without_sensitive_fields()
    {
        var deadline = CreateDeadline();
        var calendarEntryId = Guid.CreateVersion7();
        var activity = DeadlineActivity.Record(deadline, "calendar_changed", _otherUserId, LocalNow, calendarEntryId);

        activity.Id.ShouldNotBe(Guid.Empty);
        activity.DeadlineId.ShouldBe(deadline.Id);
        activity.LegalCaseId.ShouldBe(_caseId);
        ((ICaseScoped)activity).CaseId.ShouldBe(_caseId);
        activity.ActorUserId.ShouldBe(_otherUserId);
        activity.Kind.ShouldBe("calendar_changed");
        activity.CalendarEntryId.ShouldBe(calendarEntryId);
        activity.OccurredAt.ShouldBe(UtcNow);
        activity.OccurredAt.Offset.ShouldBe(TimeSpan.Zero);
        typeof(DeadlineActivity).GetProperties().Select(property => property.Name)
            .ShouldNotContain("CompletionEvidence");
        typeof(DeadlineActivity).GetProperties().Select(property => property.Name)
            .ShouldNotContain("MissedReason");
        typeof(DeadlineActivity).GetProperties().Select(property => property.Name)
            .ShouldNotContain("CalculationSnapshot");
    }

    private Deadline CreateDeadline(bool requireReview = false) =>
        Deadline.CreateManual(ValidInput(requireReview: requireReview), _userId, LocalNow).Value;

    private ManualDeadlineInput ValidInput(
        DeadlineRule? rule = null,
        bool confirmed = true,
        bool requireReview = false,
        string title = "  Submit appeal  ",
        string type = "  appeal  ",
        string calculationSnapshot = "calculation reviewed by the user",
        DateOnly? startOn = null,
        DateOnly? internalDueOn = null,
        DateOnly? fatalDueOn = null) =>
        new(_caseId, type, title, _otherUserId, rule ?? ValidRule(), startOn ?? StartOn,
            internalDueOn ?? StartOn.AddDays(4), fatalDueOn ?? StartOn.AddDays(10), calculationSnapshot,
            Guid.CreateVersion7(), "SP", "São Paulo", requireReview, confirmed);

    private static DeadlineRule ValidRule(
        DeadlineStartKind startKind = DeadlineStartKind.EventDate,
        CountingMode mode = CountingMode.BusinessDays,
        int days = 10,
        int internalLeadDays = 4,
        DateOnly? triggerDate = null) =>
        new(startKind, triggerDate ?? StartOn, null, days, mode, Doubled: false, internalLeadDays);
}

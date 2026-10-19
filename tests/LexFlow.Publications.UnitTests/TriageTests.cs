using LexFlow.Publications.Application.Closing;
using LexFlow.Publications.Application.Triage;
using LexFlow.Publications.Domain;

namespace LexFlow.Publications.UnitTests;

public sealed class TriageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 13, 0, 0, TimeSpan.Zero);
    private readonly Guid _case = Guid.CreateVersion7();
    private readonly Guid _publication = Guid.CreateVersion7();
    private readonly Guid _user = Guid.CreateVersion7();

    [Fact]
    public void Irrelevant_publication_can_be_closed_without_case_with_reason_and_author()
    {
        var triage = PublicationTriage.Create(_publication, null, new(TriageClassification.Other, TriageAction.Discard, "  Homônimo  ", true), _user, Now).Value;

        triage.LegalCaseId.ShouldBeNull();
        triage.Reason.ShouldBe("Homônimo");
        triage.Action.ShouldBe(TriageAction.Discard);
        triage.Classification.ShouldBe(TriageClassification.Other);
        triage.TriagedBy.ShouldBe(_user);
        triage.TriagedAt.ShouldBe(Now);
        triage.PublicationId.ShouldBe(_publication);
        triage.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Discard_requires_reason(string? reason) =>
        new TriageDecision(TriageClassification.Other, TriageAction.Discard, reason, true).Validate(null).Error.ShouldBe(PublicationErrors.InvalidTriage);

    [Theory]
    [InlineData(TriageClassification.DeadlineNotice, TriageAction.NoAction)]
    [InlineData(TriageClassification.Other, TriageAction.Deadline)]
    [InlineData(TriageClassification.Hearing, TriageAction.NoAction)]
    [InlineData(TriageClassification.Routine, TriageAction.Hearing)]
    [InlineData(TriageClassification.Decision, TriageAction.Discard)]
    [InlineData((TriageClassification)99, TriageAction.Task)]
    [InlineData(TriageClassification.Other, (TriageAction)99)]
    public void Classification_and_action_must_be_consistent(TriageClassification classification, TriageAction action) =>
        new TriageDecision(classification, action, "Motivo", true).Validate(_case).Error.ShouldBe(PublicationErrors.InvalidTriage);

    [Theory]
    [InlineData(TriageClassification.Routine, TriageAction.NoAction)]
    [InlineData(TriageClassification.DeadlineNotice, TriageAction.Deadline)]
    [InlineData(TriageClassification.Hearing, TriageAction.Hearing)]
    [InlineData(TriageClassification.Decision, TriageAction.Task)]
    public void Other_triage_actions_require_linked_case(TriageClassification classification, TriageAction action) =>
        new TriageDecision(classification, action, null, true).Validate(null).Error.ShouldBe(PublicationErrors.CaseRequired);

    [Fact]
    public void Triage_requires_confirmation_and_bounded_reason()
    {
        new TriageDecision(TriageClassification.Routine, TriageAction.NoAction, null, false).Validate(_case).Error.ShouldBe(PublicationErrors.InvalidTriage);
        new TriageDecision(TriageClassification.Other, TriageAction.Discard, new string('x', 4001), true).Validate(null).Error.ShouldBe(PublicationErrors.InvalidTriage);
    }

    [Theory]
    [InlineData(TriageClassification.DeadlineNotice, TriageAction.Deadline)]
    [InlineData(TriageClassification.Decision, TriageAction.Task)]
    [InlineData(TriageClassification.Hearing, TriageAction.Hearing)]
    public void Generated_target_is_recorded_with_the_immutable_decision(TriageClassification classification, TriageAction action)
    {
        var target = Guid.CreateVersion7();
        var triage = PublicationTriage.Create(_publication, _case, new(classification, action, null, true), _user, Now,
            action == TriageAction.Deadline ? target : null, action == TriageAction.Task ? target : null, action == TriageAction.Hearing ? target : null).Value;
        var response = PublicationTriageResponse.Of(triage);

        (response.DeadlineId ?? response.TaskId ?? response.HearingId).ShouldBe(target);
        response.CaseId.ShouldBe(_case);
        response.TriagedBy.ShouldBe(_user);
    }

    [Fact]
    public void Missing_extra_or_empty_target_and_missing_author_are_rejected()
    {
        var decision = new TriageDecision(TriageClassification.DeadlineNotice, TriageAction.Deadline, null, true);
        PublicationTriage.Create(_publication, _case, decision, _user, Now).Error.ShouldBe(PublicationErrors.InvalidTriage);
        PublicationTriage.Create(_publication, _case, decision, _user, Now, Guid.CreateVersion7(), Guid.CreateVersion7()).Error.ShouldBe(PublicationErrors.InvalidTriage);
        PublicationTriage.Create(_publication, _case, decision, _user, Now, Guid.Empty).Error.ShouldBe(PublicationErrors.InvalidTriage);
        PublicationTriage.Create(Guid.Empty, _case, decision, _user, Now, Guid.CreateVersion7()).Error.ShouldBe(PublicationErrors.InvalidTriage);
        PublicationTriage.Create(_publication, _case, decision, Guid.Empty, Now, Guid.CreateVersion7()).Error.ShouldBe(PublicationErrors.InvalidTriage);
    }

    [Fact]
    public void Completed_publication_rejects_assignment_link_or_second_triage()
    {
        var day = new DateOnly(2026, 10, 19);
        var publication = Publication.Receive(new(Guid.CreateVersion7(), "DJE", day, day, "Texto"), Now).Value;
        publication.Assign(Guid.Empty).Error.ShouldBe(PublicationErrors.AssigneeUnavailable);
        publication.Assign(_user).IsSuccess.ShouldBeTrue();
        publication.AssigneeUserId.ShouldBe(_user);
        publication.Assign(null).IsSuccess.ShouldBeTrue();
        publication.CompleteTriage().IsSuccess.ShouldBeTrue();
        publication.Assign(_user).Error.ShouldBe(PublicationErrors.AlreadyTriaged);
        publication.Link(_case).Error.ShouldBe(PublicationErrors.AlreadyTriaged);
        publication.CompleteTriage().Error.ShouldBe(PublicationErrors.AlreadyTriaged);
    }

    [Fact]
    public void Closing_snapshot_becomes_pending_after_new_content_without_modifying_history()
    {
        var close = DailyClose.Create(new(2026, 10, 19), 1, _user, Now, 2, "capture");
        new ClosingSnapshot(2, 0, 0, "capture", close).IsClosed.ShouldBeTrue();
        new ClosingSnapshot(3, 0, 0, "capture", close).IsClosed.ShouldBeFalse();
        new ClosingSnapshot(2, 1, 0, "capture", close).IsClosed.ShouldBeFalse();
        new ClosingSnapshot(2, 0, 1, "capture", close).IsClosed.ShouldBeFalse();
        new ClosingSnapshot(2, 0, 0, "new-capture", close).IsClosed.ShouldBeFalse();
        new ClosingSnapshot(2, 0, 0, "capture", null).IsClosed.ShouldBeFalse();
        close.PublicationCount.ShouldBe(2);
        close.Revision.ShouldBe(1);
        close.ClosedBy.ShouldBe(_user);
    }

    [Theory]
    [InlineData(2026, 10, 19, 3)]
    [InlineData(2018, 11, 4, 3)]
    public void Legal_day_cutoff_handles_current_offset_and_historical_invalid_midnight(int year, int month, int day, int utcHour) =>
        LegalDay.Start(new(year, month, day), TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")).Hour.ShouldBe(utcHour);

    [Fact]
    public void Source_period_preserves_first_deactivation_time()
    {
        var source = Guid.CreateVersion7();
        var period = CaptureSourcePeriod.Start(source, Now);
        period.End(Now.AddHours(1));
        period.End(Now.AddHours(2));
        period.SourceId.ShouldBe(source);
        period.ActivatedAt.ShouldBe(Now);
        period.DeactivatedAt.ShouldBe(Now.AddHours(1));
    }
}

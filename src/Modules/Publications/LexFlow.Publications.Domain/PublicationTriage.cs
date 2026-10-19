using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Domain;

public enum TriageClassification { DeadlineNotice, Routine, Hearing, Decision, Judgment, Other }
public enum TriageAction { NoAction, Discard, Deadline, Task, Hearing }

public sealed record TriageDecision(TriageClassification Classification, TriageAction Action, string? Reason, bool Confirmed)
{
    public Result Validate(Guid? caseId)
    {
        if (!Confirmed || !Enum.IsDefined(Classification) || !Enum.IsDefined(Action) || Reason?.Length > 4000
            || Action == TriageAction.Discard && (Classification != TriageClassification.Other || string.IsNullOrWhiteSpace(Reason))
            || Classification == TriageClassification.DeadlineNotice && Action != TriageAction.Deadline
            || Action == TriageAction.Deadline && Classification != TriageClassification.DeadlineNotice
            || Classification == TriageClassification.Hearing && Action != TriageAction.Hearing
            || Action == TriageAction.Hearing && Classification != TriageClassification.Hearing)
        { return PublicationErrors.InvalidTriage; }
        return Action != TriageAction.Discard && (caseId is null || caseId == Guid.Empty) ? PublicationErrors.CaseRequired : Result.Success;
    }
}

/// <summary>Decisão humana imutável; o encerramento irrelevante pode ficar sem processo, com justificativa.</summary>
public sealed class PublicationTriage : Entity<Guid>, ITenantOwned, ICaseScoped
{
    private PublicationTriage() { }
    public Guid OrganizationId { get; private set; }
    public Guid PublicationId { get; private set; }
    public Guid? LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public TriageClassification Classification { get; private set; }
    public TriageAction Action { get; private set; }
    [NotAudited]
    public string? Reason { get; private set; }
    public Guid TriagedBy { get; private set; }
    public DateTimeOffset TriagedAt { get; private set; }
    public Guid? DeadlineId { get; private set; }
    public Guid? TaskId { get; private set; }
    public Guid? HearingId { get; private set; }

    public static Result<PublicationTriage> Create(Guid publicationId, Guid? caseId, TriageDecision decision,
        Guid userId, DateTimeOffset now, Guid? deadlineId = null, Guid? taskId = null, Guid? hearingId = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var validation = decision.Validate(caseId);
        if (validation.IsFailure) { return validation.Error; }
        if (publicationId == Guid.Empty || userId == Guid.Empty
            || deadlineId == Guid.Empty || taskId == Guid.Empty || hearingId == Guid.Empty
            || (decision.Action == TriageAction.Deadline) != (deadlineId is not null && deadlineId != Guid.Empty)
            || (decision.Action == TriageAction.Task) != (taskId is not null && taskId != Guid.Empty)
            || (decision.Action == TriageAction.Hearing) != (hearingId is not null && hearingId != Guid.Empty))
        { return PublicationErrors.InvalidTriage; }
        return new PublicationTriage
        {
            Id = Guid.CreateVersion7(),
            PublicationId = publicationId,
            LegalCaseId = caseId,
            Classification = decision.Classification,
            Action = decision.Action,
            Reason = string.IsNullOrWhiteSpace(decision.Reason) ? null : decision.Reason.Trim(),
            TriagedBy = userId,
            TriagedAt = now.ToUniversalTime(),
            DeadlineId = deadlineId,
            TaskId = taskId,
            HearingId = hearingId,
        };
    }
}

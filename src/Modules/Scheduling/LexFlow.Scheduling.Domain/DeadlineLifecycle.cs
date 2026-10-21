using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain;

public sealed record DeadlineRule(DeadlineStartKind StartKind, DateOnly TriggerDate, DateOnly? ConsultedOn,
    int Days, CountingMode Mode, bool Doubled, int InternalLeadDays);

public sealed record ManualDeadlineInput(Guid CaseId, string Type, string Title, Guid AssigneeUserId, DeadlineRule Rule,
    DateOnly StartOn, DateOnly InternalDueOn, DateOnly FatalDueOn, string CalculationSnapshot,
    Guid? CourtId, string? StateCode, string? Municipality, bool RequireReview, bool Confirmed);

public sealed partial class Deadline
{
    public string Type { get; private set; } = "procedural";
    public DeadlineRule? Rule { get; private set; }
    [Sensitive]
    public string? CalculationSnapshot { get; private set; }
    public Guid? CourtId { get; private set; }
    public string? StateCode { get; private set; }
    public string? Municipality { get; private set; }
    public bool RequireReview { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    [Sensitive]
    public string? CompletionEvidence { get; private set; }
    [Sensitive]
    public string? MissedReason { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset? LastCalendarAlertAt { get; private set; }

    public static Result<Deadline> CreateManual(ManualDeadlineInput input, Guid userId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!WorkItemRules.Valid(input.CaseId, input.CaseId, input.Title, input.AssigneeUserId, input.Confirmed, userId)
            || string.IsNullOrWhiteSpace(input.Type) || input.Type.Trim().Length > 80
            || input.StartOn == default || input.InternalDueOn < input.StartOn || input.FatalDueOn < input.InternalDueOn
            || !ValidRule(input.Rule) || string.IsNullOrWhiteSpace(input.CalculationSnapshot))
        {
            return SchedulingErrors.InvalidDeadline;
        }
        return new Deadline
        {
            Id = Guid.CreateVersion7(),
            LegalCaseId = input.CaseId,
            Title = input.Title.Trim(),
            Type = input.Type.Trim(),
            AssigneeUserId = input.AssigneeUserId,
            StartOn = input.StartOn,
            InternalDueOn = input.InternalDueOn,
            FatalDueOn = input.FatalDueOn,
            Rule = input.Rule,
            CalculationSnapshot = input.CalculationSnapshot,
            CourtId = input.CourtId,
            StateCode = input.StateCode,
            Municipality = input.Municipality,
            RequireReview = input.RequireReview,
            ConfirmedBy = userId,
            ConfirmedAt = now.ToUniversalTime(),
        };
    }

    private static bool ValidRule(DeadlineRule? rule) => rule is not null && Enum.IsDefined(rule.StartKind)
        && Enum.IsDefined(rule.Mode) && rule.TriggerDate != default && rule.Days is >= 1 and <= DeadlineCalculator.MaxDays
        && rule.InternalLeadDays is >= 0 and <= DeadlineCalculator.MaxInternalLeadDays;

    public void SetReviewRequirement(bool required) => RequireReview = required;

    public void FlagCalendarChange(DateTimeOffset now) => LastCalendarAlertAt = now.ToUniversalTime();

    public Result Start()
    {
        if (Status != WorkItemStatus.Pending) { return SchedulingErrors.InvalidTransition; }
        Status = WorkItemStatus.InProgress;
        return Result.Success;
    }

    public Result Review(Guid reviewerId, bool singleMember, DateTimeOffset now)
    {
        if (IsClosed || ReviewedBy is not null) { return SchedulingErrors.InvalidTransition; }
        if (reviewerId == Guid.Empty || (reviewerId == ConfirmedBy && !singleMember)) { return SchedulingErrors.SeparateReviewerRequired; }
        ReviewedBy = reviewerId;
        ReviewedAt = now.ToUniversalTime();
        return Result.Success;
    }

    public Result Complete(string evidence, Guid userId, DateTimeOffset now)
    {
        if (IsClosed) { return SchedulingErrors.InvalidTransition; }
        if (RequireReview && ReviewedBy is null) { return SchedulingErrors.ReviewRequired; }
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(evidence) || evidence.Trim().Length > 4000) { return SchedulingErrors.EvidenceRequired; }
        CompletionEvidence = evidence.Trim();
        Close(WorkItemStatus.Completed, userId, now);
        return Result.Success;
    }

    public Result MarkMissed(string reason, Guid userId, DateTimeOffset now)
    {
        if (IsClosed) { return SchedulingErrors.InvalidTransition; }
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 4000) { return SchedulingErrors.MissedReasonRequired; }
        MissedReason = reason.Trim();
        Close(WorkItemStatus.Missed, userId, now);
        return Result.Success;
    }

    private bool IsClosed => Status is WorkItemStatus.Completed or WorkItemStatus.Missed;
    private void Close(WorkItemStatus status, Guid userId, DateTimeOffset now)
    {
        Status = status;
        ClosedBy = userId;
        ClosedAt = now.ToUniversalTime();
    }
}

/// <summary>Histórico imutável de transições e alertas; não duplica protocolos nem justificativas na timeline.</summary>
public sealed class DeadlineActivity : Entity<Guid>, ITenantOwned, ICaseScoped
{
    private DeadlineActivity() { }
    public Guid OrganizationId { get; private set; }
    public Guid DeadlineId { get; private set; }
    public Guid LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public string Kind { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? CalendarEntryId { get; private set; }

    public static DeadlineActivity Record(Deadline deadline, string kind, Guid actor, DateTimeOffset now, Guid? calendarEntryId = null)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        return new DeadlineActivity
        {
            Id = Guid.CreateVersion7(),
            DeadlineId = deadline.Id,
            LegalCaseId = deadline.LegalCaseId,
            Kind = kind,
            ActorUserId = actor,
            OccurredAt = now.ToUniversalTime(),
            CalendarEntryId = calendarEntryId
        };
    }
}

public static partial class SchedulingErrors
{
    public static readonly Error DeadlineNotFound = Error.NotFound("scheduling.deadline_not_found", "Prazo não encontrado.");
    public static readonly Error InvalidTransition = Error.Conflict("scheduling.deadline_transition_invalid", "A situação do prazo não permite esta operação.");
    public static readonly Error ReviewRequired = Error.BusinessRule("scheduling.deadline_review_required", "O prazo exige conferência antes do cumprimento.");
    public static readonly Error SeparateReviewerRequired = Error.BusinessRule("scheduling.separate_reviewer_required", "A conferência exige outro membro, exceto em escritório com um único membro ativo.");
    public static readonly Error EvidenceRequired = Error.Validation("scheduling.evidence_required", "Informe o protocolo de cumprimento, com até 4000 caracteres.");
    public static readonly Error MissedReasonRequired = Error.Validation("scheduling.missed_reason_required", "Justifique a perda do prazo, com até 4000 caracteres.");
    public static readonly Error CalculationChanged = Error.Conflict("scheduling.calculation_changed", "O cálculo atual difere das datas confirmadas. Confira a sugestão novamente.");
}

using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Publications.Domain;
using LexFlow.Scheduling.Contracts;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Application.Triage;

internal sealed record TriageTarget(Guid? DeadlineId = null, Guid? TaskId = null, Guid? HearingId = null);

internal sealed class PublicationTriageTargets(ISchedulingIntake scheduling, IPublicationHearings hearings,
    ICaseDirectory cases, IOrganizationAccessDirectory members)
{
    public async Task<Result<TriageTarget>> CreateAsync(Publication publication, PublicationTriageInput input, CancellationToken cancellationToken)
    {
        var caseId = publication.LegalCaseId ?? Guid.Empty;
        if (input.Action == TriageAction.Deadline && input.Deadline is { } d)
        {
            var result = await scheduling.CreateDeadlineAsync(new DeadlineDraft(caseId, publication.Id, d.Title,
                d.StartOn, d.InternalDueOn, d.FatalDueOn, d.AssigneeUserId, input.Confirmed), cancellationToken);
            return result.IsFailure ? result.Error : new TriageTarget(DeadlineId: result.Value);
        }
        if (input.Action == TriageAction.Task && input.Task is { } t)
        {
            var result = await scheduling.CreateTaskAsync(new TaskDraft(caseId, publication.Id, t.Title, t.DueOn, t.AssigneeUserId, input.Confirmed), cancellationToken);
            return result.IsFailure ? result.Error : new TriageTarget(TaskId: result.Value);
        }
        if (input.Action == TriageAction.Hearing && input.Hearing is { } h)
        {
            if (await members.GetAsync(h.LawyerUserId, cancellationToken) is null
                || !(await cases.ScopeForUserAsync(h.LawyerUserId, "cases.read", cancellationToken)).VisibleIds.Contains(caseId))
            { return PublicationErrors.AssigneeUnavailable; }
            var result = await hearings.CreateAsync(new PublicationHearingDraft(caseId, h.Type, h.Format, h.ScheduledAt,
                h.Location, h.MeetingLink, h.LawyerUserId, h.Notes), cancellationToken);
            return result.IsFailure ? result.Error : new TriageTarget(HearingId: result.Value);
        }
        return new TriageTarget();
    }
}

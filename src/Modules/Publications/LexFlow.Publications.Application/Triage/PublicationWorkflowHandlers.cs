using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Application.Triage;

internal sealed class LinkPublicationHandler(IPublicationsDbContext db, PublicationWorkflowAccess access, ICaptureLocks locks)
    : ICommandHandler<LinkPublicationCommand, PublicationWorkflowResponse>
{
    public async Task<Result<PublicationWorkflowResponse>> Handle(LinkPublicationCommand command, CancellationToken cancellationToken)
    {
        await locks.ControlAsync(cancellationToken);
        var existing = await access.GetAsync(command.Id, "publications.triage", cancellationToken, tracking: true);
        if (existing.IsFailure) { return existing.Error; }
        var publication = existing.Value;
        var allowed = await access.LinkAllowedAsync(command.CaseId, cancellationToken);
        if (allowed.IsFailure) { return allowed.Error; }
        if (publication.AssigneeUserId is { } user && !await access.AssigneeAllowedAsync(publication, user, command.CaseId, cancellationToken))
        { return PublicationErrors.AssigneeUnavailable; }
        var linked = publication.Link(command.CaseId);
        if (linked.IsFailure) { return linked.Error; }
        await db.SaveChangesAsync(cancellationToken);
        return new PublicationWorkflowResponse(publication.Id, publication.LegalCaseId, publication.AssigneeUserId, publication.Status, null);
    }
}

internal sealed class AssignPublicationHandler(IPublicationsDbContext db, PublicationWorkflowAccess access, ICaptureLocks locks)
    : ICommandHandler<AssignPublicationCommand, PublicationWorkflowResponse>
{
    public async Task<Result<PublicationWorkflowResponse>> Handle(AssignPublicationCommand command, CancellationToken cancellationToken)
    {
        await locks.ControlAsync(cancellationToken);
        var existing = await access.GetAsync(command.Id, "publications.distribute", cancellationToken, tracking: true);
        if (existing.IsFailure) { return existing.Error; }
        var publication = existing.Value;
        if (command.AssigneeUserId is { } user && !await access.AssigneeAllowedAsync(publication, user, publication.LegalCaseId, cancellationToken))
        { return PublicationErrors.AssigneeUnavailable; }
        var assigned = publication.Assign(command.AssigneeUserId);
        if (assigned.IsFailure) { return assigned.Error; }
        await db.SaveChangesAsync(cancellationToken);
        return new PublicationWorkflowResponse(publication.Id, publication.LegalCaseId, publication.AssigneeUserId, publication.Status, null);
    }
}

internal sealed class TriagePublicationHandler(IPublicationsDbContext db, PublicationWorkflowAccess access, ICaptureLocks locks,
    PublicationTriageTargets targets, ICurrentUser user, IClock clock) : ICommandHandler<TriagePublicationCommand, PublicationWorkflowResponse>
{
    public async Task<Result<PublicationWorkflowResponse>> Handle(TriagePublicationCommand command, CancellationToken cancellationToken)
    {
        await locks.ControlAsync(cancellationToken);
        var existing = await access.GetAsync(command.Id, "publications.triage", cancellationToken, tracking: true);
        if (existing.IsFailure) { return existing.Error; }
        var publication = existing.Value;
        if (publication.Status != PublicationStatus.Pending) { return PublicationErrors.AlreadyTriaged; }
        var prepared = await PrepareAsync(publication, command.Input, cancellationToken);
        if (prepared.IsFailure) { return prepared.Error; }
        var target = await targets.CreateAsync(publication, command.Input, cancellationToken);
        if (target.IsFailure) { return target.Error; }
        var triage = PublicationTriage.Create(publication.Id, publication.LegalCaseId, command.Input.ToDecision(), user.UserId ?? Guid.Empty,
            clock.UtcNow, target.Value.DeadlineId, target.Value.TaskId, target.Value.HearingId);
        if (triage.IsFailure) { return triage.Error; }
        publication.CompleteTriage();
        db.Triages.Add(triage.Value);
        await db.SaveChangesAsync(cancellationToken);
        return new PublicationWorkflowResponse(publication.Id, publication.LegalCaseId, publication.AssigneeUserId,
            publication.Status, PublicationTriageResponse.Of(triage.Value));
    }

    private async Task<Result> PrepareAsync(Publication publication, PublicationTriageInput input, CancellationToken cancellationToken)
    {
        var caseId = input.CaseId ?? publication.LegalCaseId;
        var valid = input.ToDecision().Validate(caseId);
        if (valid.IsFailure) { return valid; }
        if (input.CaseId is not { } linked) { return Result.Success; }
        var allowed = await access.LinkAllowedAsync(linked, cancellationToken);
        if (allowed.IsFailure) { return allowed; }
        if (publication.AssigneeUserId is { } userId && !await access.AssigneeAllowedAsync(publication, userId, linked, cancellationToken))
        { return PublicationErrors.AssigneeUnavailable; }
        return publication.Link(linked);
    }
}

using FluentValidation;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Application.Triage;

public sealed record DeadlineTriageInput(string Title, DateOnly StartOn, DateOnly InternalDueOn, DateOnly FatalDueOn, Guid AssigneeUserId);
public sealed record TaskTriageInput(string Title, DateOnly DueOn, Guid AssigneeUserId);
public sealed record HearingTriageInput(PublicationHearingType Type, PublicationHearingFormat Format, DateTimeOffset ScheduledAt,
    string? Location, string? MeetingLink, Guid LawyerUserId, string? Notes);
public sealed record PublicationTriageInput(TriageClassification Classification, TriageAction Action, bool Confirmed,
    string? Reason = null, Guid? CaseId = null, DeadlineTriageInput? Deadline = null, TaskTriageInput? Task = null, HearingTriageInput? Hearing = null)
{
    public TriageDecision ToDecision() => new(Classification, Action, Reason, Confirmed);
    public bool HasValidTarget() => (Action == TriageAction.Deadline) == (Deadline is not null)
        && (Action == TriageAction.Task) == (Task is not null) && (Action == TriageAction.Hearing) == (Hearing is not null);
}
public sealed record PublicationTriageResponse(Guid Id, TriageClassification Classification, TriageAction Action, string? Reason,
    Guid? CaseId, Guid TriagedBy, DateTimeOffset TriagedAt, Guid? DeadlineId, Guid? TaskId, Guid? HearingId)
{
    public static PublicationTriageResponse Of(PublicationTriage t) => new(t.Id, t.Classification, t.Action, t.Reason,
        t.LegalCaseId, t.TriagedBy, t.TriagedAt, t.DeadlineId, t.TaskId, t.HearingId);
}
public sealed record PublicationWorkflowResponse(Guid Id, Guid? CaseId, Guid? AssigneeUserId, PublicationStatus Status, PublicationTriageResponse? Triage);
public sealed record LinkPublicationCommand(Guid Id, Guid CaseId) : ICommand<PublicationWorkflowResponse>;
public sealed record AssignPublicationCommand(Guid Id, Guid? AssigneeUserId) : ICommand<PublicationWorkflowResponse>;
public sealed record TriagePublicationCommand(Guid Id, PublicationTriageInput Input) : ICommand<PublicationWorkflowResponse>, ILocalTransactionCommand
{
    private static readonly IReadOnlyList<string> Modules = Array.AsReadOnly(new[] { "Publications", "Scheduling", "Litigation" });
    public IReadOnlyList<string> TransactionModules => Modules;
}

internal sealed class TriagePublicationValidator : AbstractValidator<TriagePublicationCommand>
{
    public TriagePublicationValidator()
    {
        RuleFor(c => c.Input).NotNull();
        When(c => c.Input is not null, () =>
        {
            RuleFor(c => c.Input.Classification).IsInEnum().WithErrorCode("publications.triage_invalid");
            RuleFor(c => c.Input.Action).IsInEnum().WithErrorCode("publications.triage_invalid");
            RuleFor(c => c.Input).Must(i => i.HasValidTarget()).WithErrorCode("publications.triage_invalid");
        });
    }
}

internal sealed class PublicationWorkflowAuthorizers(PublicationWorkflowAccess access) :
    IResourceAuthorizer<LinkPublicationCommand>, IResourceAuthorizer<AssignPublicationCommand>, IResourceAuthorizer<TriagePublicationCommand>
{
    public async Task<Result> AuthorizeAsync(LinkPublicationCommand request, CancellationToken cancellationToken)
    {
        var existing = await access.GetAsync(request.Id, "publications.triage", cancellationToken);
        return existing.IsFailure ? existing.Error : await access.LinkAllowedAsync(request.CaseId, cancellationToken);
    }
    public async Task<Result> AuthorizeAsync(AssignPublicationCommand request, CancellationToken cancellationToken)
    {
        var existing = await access.GetAsync(request.Id, "publications.distribute", cancellationToken);
        return existing.IsFailure ? existing.Error : Result.Success;
    }
    public async Task<Result> AuthorizeAsync(TriagePublicationCommand request, CancellationToken cancellationToken)
    {
        var existing = await access.GetAsync(request.Id, "publications.triage", cancellationToken);
        if (existing.IsFailure) { return existing.Error; }
        return request.Input.CaseId is { } caseId ? await access.LinkAllowedAsync(caseId, cancellationToken) : Result.Success;
    }
}

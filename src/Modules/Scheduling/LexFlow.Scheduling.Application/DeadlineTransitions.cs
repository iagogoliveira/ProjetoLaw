using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

public sealed record StartDeadlineCommand(Guid Id) : ICommand<DeadlineDetailResponse>;
public sealed record ReviewDeadlineCommand(Guid Id) : ICommand<DeadlineDetailResponse>;
public sealed record CompleteDeadlineCommand(Guid Id, string Evidence) : ICommand<DeadlineDetailResponse>;
public sealed record MissDeadlineCommand(Guid Id, string Reason) : ICommand<DeadlineDetailResponse>;

internal sealed class DeadlineTransitionAuthorizers(DeadlineAccess access) : IResourceAuthorizer<StartDeadlineCommand>,
    IResourceAuthorizer<ReviewDeadlineCommand>, IResourceAuthorizer<CompleteDeadlineCommand>, IResourceAuthorizer<MissDeadlineCommand>
{
    public Task<Result> AuthorizeAsync(StartDeadlineCommand request, CancellationToken cancellationToken) => access.AuthorizeAsync(request.Id, "schedule.deadlines.manage", cancellationToken);
    public Task<Result> AuthorizeAsync(ReviewDeadlineCommand request, CancellationToken cancellationToken) => access.AuthorizeAsync(request.Id, "schedule.deadlines.verify", cancellationToken);
    public Task<Result> AuthorizeAsync(CompleteDeadlineCommand request, CancellationToken cancellationToken) => access.AuthorizeAsync(request.Id, "schedule.deadlines.manage", cancellationToken);
    public Task<Result> AuthorizeAsync(MissDeadlineCommand request, CancellationToken cancellationToken) => access.AuthorizeAsync(request.Id, "schedule.deadlines.manage", cancellationToken);
}

internal sealed class DeadlineTransitions(ISchedulingDbContext db, ICurrentUser user, IClock clock, IOrganizationMembers members,
    IIntegrationEventPublisher events) : ICommandHandler<StartDeadlineCommand, DeadlineDetailResponse>,
    ICommandHandler<ReviewDeadlineCommand, DeadlineDetailResponse>, ICommandHandler<CompleteDeadlineCommand, DeadlineDetailResponse>,
    ICommandHandler<MissDeadlineCommand, DeadlineDetailResponse>
{
    public Task<Result<DeadlineDetailResponse>> Handle(StartDeadlineCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(command.Id, "scheduling.deadline_started", d => d.Start(), cancellationToken);
    public async Task<Result<DeadlineDetailResponse>> Handle(ReviewDeadlineCommand command, CancellationToken cancellationToken)
    {
        // A contagem é consultada dentro da operação; a autorização verify continua obrigatória no escritório solo.
        var single = await members.CountActiveAsync(cancellationToken) == 1;
        return await ChangeAsync(command.Id, "scheduling.deadline_reviewed", d => d.Review(user.UserId ?? Guid.Empty, single, clock.UtcNow), cancellationToken);
    }
    public Task<Result<DeadlineDetailResponse>> Handle(CompleteDeadlineCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(command.Id, "scheduling.deadline_completed", d => d.Complete(command.Evidence, user.UserId ?? Guid.Empty, clock.UtcNow), cancellationToken);
    public Task<Result<DeadlineDetailResponse>> Handle(MissDeadlineCommand command, CancellationToken cancellationToken) =>
        ChangeAsync(command.Id, "scheduling.deadline_missed", d => d.MarkMissed(command.Reason, user.UserId ?? Guid.Empty, clock.UtcNow), cancellationToken);

    private async Task<Result<DeadlineDetailResponse>> ChangeAsync(Guid id, string kind, Func<Deadline, Result> change, CancellationToken ct)
    {
        await db.LockDeadlinesAsync(ct);
        var deadline = await db.Deadlines.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (deadline is null) { return SchedulingErrors.DeadlineNotFound; }
        var result = change(deadline);
        if (result.IsFailure) { return result.Error; }
        var activity = DeadlineActivity.Record(deadline, kind, user.UserId!.Value, clock.UtcNow);
        db.DeadlineActivities.Add(activity);
        if (string.Equals(kind, "scheduling.deadline_missed", StringComparison.Ordinal))
        {
            events.Publish(new DeadlineAlertIntegrationEvent(activity.Id, deadline.Id, deadline.LegalCaseId, kind));
        }
        await db.SaveChangesAsync(ct);
        return DeadlineDetailResponse.Of(deadline);
    }
}

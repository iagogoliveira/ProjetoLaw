using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Application;

public sealed record CreateDeadlineCommand(DeadlineDraft Draft) : ICommand<Guid>;
public sealed record CreateTaskCommand(TaskDraft Draft) : ICommand<Guid>;

internal sealed class SchedulingIntake(ISchedulingDbContext db, ICommandHandler<CreateDeadlineCommand, Guid> deadlines,
    ICommandHandler<CreateTaskCommand, Guid> tasks) : ISchedulingIntake
{
    public Task<Result<Guid>> CreateDeadlineAsync(DeadlineDraft draft, CancellationToken cancellationToken)
    {
        EnsureSharedTransaction();
        return deadlines.Handle(new CreateDeadlineCommand(draft), cancellationToken);
    }

    public Task<Result<Guid>> CreateTaskAsync(TaskDraft draft, CancellationToken cancellationToken)
    {
        EnsureSharedTransaction();
        return tasks.Handle(new CreateTaskCommand(draft), cancellationToken);
    }

    private void EnsureSharedTransaction()
    {
        if (!db.HasActiveTransaction) { throw new InvalidOperationException("Entrada de publicação exige a transação local compartilhada."); }
    }
}

internal sealed class WorkCreationAuthorizers(ICaseDirectory cases) :
    IResourceAuthorizer<CreateDeadlineCommand>, IResourceAuthorizer<CreateTaskCommand>
{
    public Task<Result> AuthorizeAsync(CreateDeadlineCommand request, CancellationToken cancellationToken) =>
        cases.AuthorizeAsync(request.Draft.CaseId, "schedule.deadlines.manage", cancellationToken);
    public Task<Result> AuthorizeAsync(CreateTaskCommand request, CancellationToken cancellationToken) =>
        cases.AuthorizeAsync(request.Draft.CaseId, "schedule.tasks.manage", cancellationToken);
}

internal sealed class WorkAssignees(ICaseDirectory cases, IOrganizationAccessDirectory members)
{
    public async Task<Result> CheckAsync(Guid caseId, Guid userId, string readPermission, CancellationToken cancellationToken)
    {
        if (!await cases.IsOpenAsync(caseId, cancellationToken)) { return SchedulingErrors.CaseUnavailable; }
        if (await members.GetAsync(userId, cancellationToken) is null) { return SchedulingErrors.AssigneeUnavailable; }
        var scope = await cases.ScopeForUserAsync(userId, readPermission, cancellationToken);
        var readable = await cases.ScopeForUserAsync(userId, "cases.read", cancellationToken);
        return scope.VisibleIds.Contains(caseId) && readable.VisibleIds.Contains(caseId) ? Result.Success : SchedulingErrors.AssigneeUnavailable;
    }
}

internal sealed class CreateDeadlineHandler(ISchedulingDbContext db, WorkAssignees assignees, ICurrentUser user, IClock clock)
    : ICommandHandler<CreateDeadlineCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateDeadlineCommand command, CancellationToken cancellationToken)
    {
        var d = command.Draft;
        var value = Deadline.Create(d.CaseId, d.PublicationId, d.Title, d.StartOn, d.InternalDueOn, d.FatalDueOn,
            d.AssigneeUserId, d.Confirmed, user.UserId ?? Guid.Empty, clock.UtcNow);
        if (value.IsFailure) { return value.Error; }
        var allowed = await assignees.CheckAsync(d.CaseId, d.AssigneeUserId, "schedule.deadlines.read", cancellationToken);
        if (allowed.IsFailure) { return allowed.Error; }
        db.Deadlines.Add(value.Value);
        await db.SaveChangesAsync(cancellationToken);
        return value.Value.Id;
    }
}

internal sealed class CreateTaskHandler(ISchedulingDbContext db, WorkAssignees assignees, ICurrentUser user, IClock clock)
    : ICommandHandler<CreateTaskCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        var d = command.Draft;
        var value = TaskItem.Create(d.CaseId, d.PublicationId, d.Title, d.DueOn, d.AssigneeUserId, d.Confirmed, user.UserId ?? Guid.Empty, clock.UtcNow);
        if (value.IsFailure) { return value.Error; }
        var allowed = await assignees.CheckAsync(d.CaseId, d.AssigneeUserId, "schedule.tasks.read", cancellationToken);
        if (allowed.IsFailure) { return allowed.Error; }
        db.Tasks.Add(value.Value);
        await db.SaveChangesAsync(cancellationToken);
        return value.Value.Id;
    }
}

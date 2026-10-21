using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

public sealed record RedistributionInput(IReadOnlyList<Guid> Ids, Guid AssigneeUserId);
public sealed record RedistributeWorkCommand(AgendaKind Kind, RedistributionInput Input) : ICommand<RedistributionResponse>;
public sealed record RedistributionItem(Guid Id, bool Changed, string? ErrorCode);
public sealed record RedistributionResponse(IReadOnlyList<RedistributionItem> Items);

internal sealed class RedistributeWorkHandler(ISchedulingDbContext db, AgendaAccess access, TaskWrites tasks,
    ICurrentUser user, IClock clock) : ICommandHandler<RedistributeWorkCommand, RedistributionResponse>
{
    public async Task<Result<RedistributionResponse>> Handle(RedistributeWorkCommand command, CancellationToken ct)
    {
        var input = command.Input;
        if (input.Ids is null || input.Ids.Count is < 1 or > 1000 || input.Ids.Distinct().Count() != input.Ids.Count
            || input.Ids.Contains(Guid.Empty) || input.AssigneeUserId == Guid.Empty || command.Kind is AgendaKind.Hearing || !Enum.IsDefined(command.Kind))
        { return SchedulingErrors.InvalidBatch; }
        await db.LockDeadlinesAsync(ct);
        var results = new List<RedistributionItem>();
        foreach (var id in input.Ids)
        {
            var result = command.Kind switch
            {
                AgendaKind.Task => await TaskAsync(id, input.AssigneeUserId, ct),
                AgendaKind.Event => await EventAsync(id, input.AssigneeUserId, ct),
                _ => await DeadlineAsync(id, input.AssigneeUserId, ct),
            };
            results.Add(new RedistributionItem(id, result.IsSuccess, result.IsFailure ? result.Error.Code : null));
        }
        await db.SaveChangesAsync(ct);
        return new RedistributionResponse(results);
    }

    private async Task<Result> TaskAsync(Guid id, Guid assignee, CancellationToken ct)
    {
        var task = await tasks.LoadAsync(id, ct);
        if (task.IsFailure) { return task.Error; }
        var allowed = await access.AuthorizeAsync(task.Value.LegalCaseId, assignee, "schedule.tasks.manage", SchedulingErrors.TaskNotFound, ct);
        if (allowed.IsFailure) { return allowed.Error; }
        allowed = await access.AssigneeAsync(task.Value.LegalCaseId, assignee, "schedule.tasks.read", ct);
        return allowed.IsFailure ? allowed.Error : task.Value.Reassign(assignee);
    }

    private async Task<Result> EventAsync(Guid id, Guid assignee, CancellationToken ct)
    {
        var allowed = await access.EventAsync(id, "schedule.events.manage", ct);
        if (allowed.IsFailure) { return allowed.Error; }
        var entry = await db.Events.SingleAsync(e => e.Id == id, ct);
        allowed = await access.AuthorizeAsync(entry.LegalCaseId, assignee, "schedule.events.manage", SchedulingErrors.EventNotFound, ct);
        if (allowed.IsFailure) { return allowed.Error; }
        allowed = await access.AssigneeAsync(entry.LegalCaseId, assignee, "schedule.events.read", ct);
        return allowed.IsFailure ? allowed.Error : entry.Reassign(assignee);
    }

    private async Task<Result> DeadlineAsync(Guid id, Guid assignee, CancellationToken ct)
    {
        var deadline = await db.Deadlines.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (deadline is null) { return SchedulingErrors.DeadlineNotFound; }
        var allowed = await access.AuthorizeAsync(deadline.LegalCaseId, deadline.AssigneeUserId, "schedule.deadlines.manage", SchedulingErrors.DeadlineNotFound, ct);
        if (allowed.IsFailure) { return allowed.Error; }
        allowed = await access.AssigneeAsync(deadline.LegalCaseId, assignee, "schedule.deadlines.read", ct);
        if (allowed.IsFailure) { return allowed.Error; }
        var result = deadline.Reassign(assignee);
        if (result.IsSuccess) { db.DeadlineActivities.Add(DeadlineActivity.Record(deadline, "scheduling.deadline_reassigned", user.UserId ?? Guid.Empty, clock.UtcNow)); }
        return result;
    }
}

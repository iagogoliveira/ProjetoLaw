using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

public sealed record ListTasksQuery(Guid? CaseId, Guid? AssigneeUserId, Guid? TeamId, Guid? ParentTaskId,
    TaskState? Status, TaskPriority? Priority, Guid? After, int Limit = 50) : IQuery<TaskListResponse>;
public sealed record TaskListResponse(IReadOnlyList<TaskDetailResponse> Items, Guid? NextCursor);

internal sealed class ListTasksHandler(ISchedulingDbContext db, AgendaAccess access, AgendaFilters filters) : IQueryHandler<ListTasksQuery, TaskListResponse>
{
    public async Task<Result<TaskListResponse>> Handle(ListTasksQuery query, CancellationToken ct)
    {
        if (query.Limit is < 1 or > 100 || (query.Status is { } state && !Enum.IsDefined(state))
            || (query.Priority is { } priority && !Enum.IsDefined(priority))) { return SchedulingErrors.InvalidAgenda; }
        var scope = await access.ScopeAsync("schedule.tasks.read", ct);
        var selected = await filters.UsersAsync(query.AssigneeUserId, query.TeamId, ct);
        var source = db.Tasks.AsNoTracking().Where(t => t.LegalCaseId != null ? scope.Cases.Contains(t.LegalCaseId.Value)
            : scope.AllAdministrative || scope.Users.Contains(t.AssigneeUserId));
        if (selected is not null) { source = source.Where(t => selected.Contains(t.AssigneeUserId)); }
        if (query.CaseId is { } caseId) { source = source.Where(t => t.LegalCaseId == caseId); }
        if (query.ParentTaskId is { } parent) { source = source.Where(t => t.ParentTaskId == parent); }
        if (query.Status is { } status) { source = source.Where(t => t.Status == status); }
        if (query.Priority is { } level) { source = source.Where(t => t.Priority == level); }
        if (query.After is { } after) { source = source.Where(t => t.Id.CompareTo(after) > 0); }
        var tasks = await source.OrderBy(t => t.Id).Take(query.Limit + 1).Include(t => t.Checklist).Include(t => t.Comments).ToListAsync(ct);
        return new TaskListResponse(tasks.Take(query.Limit).Select(TaskDetailResponse.Of).ToArray(), tasks.Count > query.Limit ? tasks[query.Limit - 1].Id : null);
    }
}

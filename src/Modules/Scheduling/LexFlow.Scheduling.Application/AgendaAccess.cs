using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

internal sealed record WorkScope(bool AllAdministrative, IReadOnlyCollection<Guid> Users, IReadOnlyCollection<Guid> Cases)
{
    public bool Contains(Guid? caseId, Guid assignee) => caseId is { } id ? Cases.Contains(id) : AllAdministrative || Users.Contains(assignee);
}

internal sealed class AgendaAccess(ICurrentAccess access, ICurrentUser user, IOrganizationDirectory organization,
    IOrganizationAccessDirectory members, ICaseDirectory cases, ISchedulingDbContext db)
{
    public async Task<WorkScope> ScopeAsync(string permission, CancellationToken ct)
    {
        var grants = await access.ScopesForAsync(permission, ct);
        var teams = grants.Where(g => g.Scope == AccessScope.Team && g.ReferenceId != null).Select(g => g.ReferenceId!.Value).ToArray();
        var users = (await organization.UsersOfTeamsAsync(teams, ct)).ToHashSet();
        if (grants.Any(g => g.Scope == AccessScope.Own) && user.UserId is { } id) { users.Add(id); }
        var reachable = await cases.VisibleIdsAsync(permission, ct);
        var readable = await cases.VisibleIdsAsync("cases.read", ct);
        return new WorkScope(grants.Any(g => g.Scope == AccessScope.Organization), users, reachable.Intersect(readable).ToArray());
    }

    public async Task<Result> AuthorizeAsync(Guid? caseId, Guid assignee, string permission, Error missing, CancellationToken ct)
    {
        if (caseId is { } id)
        {
            var allowed = await cases.AuthorizeAsync(id, permission, ct);
            if (allowed.IsFailure) { return missing; }
            var readable = await cases.AuthorizeAsync(id, "cases.read", ct);
            return readable.IsSuccess ? Result.Success : missing;
        }
        return (await ScopeAsync(permission, ct)).Contains(null, assignee) ? Result.Success : missing;
    }

    public async Task<Result> TaskAsync(Guid id, string permission, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().Where(t => t.Id == id).Select(t => new { t.LegalCaseId, t.AssigneeUserId }).SingleOrDefaultAsync(ct);
        return task is null ? SchedulingErrors.TaskNotFound
            : await AuthorizeAsync(task.LegalCaseId, task.AssigneeUserId, permission, SchedulingErrors.TaskNotFound, ct);
    }

    public async Task<Result> EventAsync(Guid id, string permission, CancellationToken ct)
    {
        var entry = await db.Events.AsNoTracking().Where(e => e.Id == id).Select(e => new { e.LegalCaseId, e.AssigneeUserId }).SingleOrDefaultAsync(ct);
        return entry is null ? SchedulingErrors.EventNotFound
            : await AuthorizeAsync(entry.LegalCaseId, entry.AssigneeUserId, permission, SchedulingErrors.EventNotFound, ct);
    }

    public async Task<Result> AssigneeAsync(Guid? caseId, Guid assignee, string permission, CancellationToken ct)
    {
        var member = await members.GetAsync(assignee, ct);
        if (member is null || !member.Permissions.TryGetValue(permission, out var grants) || grants.Count == 0) { return SchedulingErrors.AssigneeUnavailable; }
        if (caseId is null)
        {
            if (grants.Any(g => g.Scope is OfficeAccessScope.Own or OfficeAccessScope.Organization)) { return Result.Success; }
            var teams = grants.Where(g => g.Scope == OfficeAccessScope.Team && g.ReferenceId != null).Select(g => g.ReferenceId!.Value).ToArray();
            var reachableUsers = await organization.UsersOfTeamsAsync(teams, ct);
            return reachableUsers.Contains(assignee) ? Result.Success : SchedulingErrors.AssigneeUnavailable;
        }
        if (!await cases.IsOpenAsync(caseId.Value, ct)) { return SchedulingErrors.CaseUnavailable; }
        var reachable = await cases.ScopeForUserAsync(assignee, permission, ct);
        var readable = await cases.ScopeForUserAsync(assignee, "cases.read", ct);
        return reachable.VisibleIds.Contains(caseId.Value) && readable.VisibleIds.Contains(caseId.Value)
            ? Result.Success : SchedulingErrors.AssigneeUnavailable;
    }
}

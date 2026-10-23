using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;

namespace LexFlow.Scheduling.Application.CalendarSync;

internal sealed class CalendarSyncAccess(IOrganizationAccessDirectory members, IOrganizationDirectory organization, ICaseDirectory cases,
    IPermissionChecker permissions, ITenantContext tenant)
{
    public async Task<WorkScope> ScopeAsync(Guid userId, string permission, CancellationToken ct)
    {
        var permitted = await cases.ScopeForUserAsync(userId, permission, ct);
        var readable = await cases.ScopeForUserAsync(userId, "cases.read", ct);
        return new WorkScope(await CanUseAsync(userId, null, permission, ct), [], permitted.VisibleIds.Intersect(readable.VisibleIds).ToArray());
    }

    public async Task<bool> CanConnectAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty || await members.GetAsync(userId, ct) is null) { return false; }
        // Use the central permission policy: universal permissions do not appear in the directory's role grants.
        var decision = await permissions.CheckAsync(new PermissionCheck(userId, tenant.RequiredOrganizationId,
            "identity.profile.manage", MultiFactorAuthenticated: false), ct);
        return decision == PermissionDecision.Allowed;
    }

    public async Task<bool> CanUseAsync(Guid userId, Guid? caseId, string permission, CancellationToken ct)
    {
        var member = await members.GetAsync(userId, ct);
        if (member is null || !member.Permissions.TryGetValue(permission, out var grants) || grants.Count == 0) { return false; }
        if (caseId is { } id)
        {
            var permitted = await cases.ScopeForUserAsync(userId, permission, ct);
            var readable = await cases.ScopeForUserAsync(userId, "cases.read", ct);
            return permitted.VisibleIds.Contains(id) && readable.VisibleIds.Contains(id);
        }
        if (grants.Any(g => g.Scope is OfficeAccessScope.Own or OfficeAccessScope.Organization)) { return true; }
        var teams = grants.Where(g => g.Scope == OfficeAccessScope.Team && g.ReferenceId != null).Select(g => g.ReferenceId!.Value).ToArray();
        return (await organization.UsersOfTeamsAsync(teams, ct)).Contains(userId);
    }

    public async Task<bool> CanEditEventAsync(Guid userId, Guid? caseId, CancellationToken ct) =>
        await CanUseAsync(userId, caseId, "schedule.events.read", ct)
        && await CanUseAsync(userId, caseId, "schedule.events.manage", ct)
        && (caseId is null || await cases.IsOpenAsync(caseId.Value, ct));
}

using LexFlow.Organization.Contracts.OrgStructure;

namespace LexFlow.Scheduling.Application;

internal sealed class AgendaFilters(IOrganizationDirectory organization)
{
    public async Task<IReadOnlyCollection<Guid>?> UsersAsync(Guid? userId, Guid? teamId, CancellationToken ct)
    {
        IReadOnlyCollection<Guid>? result = null;
        if (teamId is { } team) { result = await organization.UsersOfTeamsAsync([team], ct); }
        if (userId is { } user) { result = result is null || result.Contains(user) ? [user] : []; }
        return result;
    }
}

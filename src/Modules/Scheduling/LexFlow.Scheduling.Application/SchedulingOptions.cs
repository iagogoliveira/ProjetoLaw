using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Application;

public sealed record SchedulingMemberOption(Guid UserId, string Name);
public sealed record SchedulingTeamOption(Guid Id, string Name);
public sealed record SchedulingOptionsResponse(IReadOnlyList<SchedulingMemberOption> Members, IReadOnlyList<SchedulingTeamOption> Teams);
public sealed record GetSchedulingOptionsQuery : IQuery<SchedulingOptionsResponse>;

internal sealed class GetSchedulingOptionsHandler(IOrganizationDirectory organization, IUserDirectory users)
    : IQueryHandler<GetSchedulingOptionsQuery, SchedulingOptionsResponse>
{
    public async Task<Result<SchedulingOptionsResponse>> Handle(GetSchedulingOptionsQuery query, CancellationToken ct)
    {
        var members = await users.GetManyAsync(await organization.MemberUserIdsAsync(ct), ct);
        var teams = await organization.ListAsync(OrgUnitKind.Team, ct);
        return new SchedulingOptionsResponse(
            [.. members.OrderBy(m => m.FullName, StringComparer.CurrentCulture).Select(m => new SchedulingMemberOption(m.Id, m.FullName))],
            [.. teams.OrderBy(t => t.Name, StringComparer.CurrentCulture).Select(t => new SchedulingTeamOption(t.Id, t.Name))]);
    }
}

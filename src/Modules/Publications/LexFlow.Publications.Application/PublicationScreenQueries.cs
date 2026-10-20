using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Identity.Contracts;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

public sealed record PublicationPendingCountResponse(int Count);
public sealed record PublicationMemberOption(Guid UserId, string Name);
public sealed record PublicationScreenOptionsResponse(IReadOnlyList<PublicationMemberOption> Members, IReadOnlyList<PublicationCaseOption> Cases);
public sealed record GetPublicationPendingCountQuery : IQuery<PublicationPendingCountResponse>;
public sealed record GetPublicationScreenOptionsQuery(string? Term) : IQuery<PublicationScreenOptionsResponse>;

internal sealed class PublicationOptionsValidator : AbstractValidator<GetPublicationScreenOptionsQuery>
{
    public PublicationOptionsValidator() => RuleFor(q => q.Term).MaximumLength(200).WithErrorCode("publications.search_too_long");
}

internal sealed class GetPublicationPendingCountHandler(PublicationAccess access) : IQueryHandler<GetPublicationPendingCountQuery, PublicationPendingCountResponse>
{
    public async Task<Result<PublicationPendingCountResponse>> Handle(GetPublicationPendingCountQuery query, CancellationToken cancellationToken) =>
        new PublicationPendingCountResponse(await (await access.VisibleAsync(cancellationToken)).CountAsync(p => p.Status == PublicationStatus.Pending, cancellationToken));
}

internal sealed class GetPublicationScreenOptionsHandler(IOrganizationDirectory organization, IUserDirectory users,
    ICaseDirectory cases, ICurrentAccess access) : IQueryHandler<GetPublicationScreenOptionsQuery, PublicationScreenOptionsResponse>
{
    public async Task<Result<PublicationScreenOptionsResponse>> Handle(GetPublicationScreenOptionsQuery query, CancellationToken cancellationToken)
    {
        var members = await users.GetManyAsync(await organization.MemberUserIdsAsync(cancellationToken), cancellationToken);
        var canTriage = (await access.ScopesForAsync("publications.triage", cancellationToken)).Count > 0;
        return new PublicationScreenOptionsResponse(
            members.OrderBy(m => m.FullName, StringComparer.CurrentCulture).Select(m => new PublicationMemberOption(m.Id, m.FullName)).ToList(),
            canTriage ? await cases.PublicationOptionsAsync(query.Term, cancellationToken) : []);
    }
}

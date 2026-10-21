using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Organization.Application.OrgStructure;

public sealed record DeadlineReviewSettingsResponse(bool RequireDeadlineReview);

public sealed record GetDeadlineReviewSettingsQuery : IQuery<DeadlineReviewSettingsResponse>;

public sealed record SetDeadlineReviewSettingsCommand(bool RequireDeadlineReview) : ICommand<DeadlineReviewSettingsResponse>;

internal sealed class GetDeadlineReviewSettingsHandler(IOrganizationDbContext db, ITenantContext tenant)
    : IQueryHandler<GetDeadlineReviewSettingsQuery, DeadlineReviewSettingsResponse>
{
    public async Task<Result<DeadlineReviewSettingsResponse>> Handle(GetDeadlineReviewSettingsQuery query, CancellationToken cancellationToken) =>
        await db.Organizations.AsNoTracking()
            .Where(organization => organization.Id == tenant.RequiredOrganizationId)
            .Select(organization => new DeadlineReviewSettingsResponse(organization.RequireDeadlineReview))
            .SingleAsync(cancellationToken);
}

internal sealed class SetDeadlineReviewSettingsHandler(IOrganizationDbContext db, ITenantContext tenant)
    : ICommandHandler<SetDeadlineReviewSettingsCommand, DeadlineReviewSettingsResponse>
{
    public async Task<Result<DeadlineReviewSettingsResponse>> Handle(SetDeadlineReviewSettingsCommand command, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.SingleAsync(o => o.Id == tenant.RequiredOrganizationId, cancellationToken);
        organization.SetDeadlineReviewRequired(command.RequireDeadlineReview);
        await db.SaveChangesAsync(cancellationToken);
        return new DeadlineReviewSettingsResponse(organization.RequireDeadlineReview);
    }
}

/// <summary>Consulta a política usando somente a organização ativa resolvida para a requisição.</summary>
internal sealed class DeadlineReviewPolicy(IOrganizationDbContext db, ITenantContext tenant) : IDeadlineReviewPolicy
{
    public Task<bool> RequiredAsync(CancellationToken cancellationToken) => db.Organizations.AsNoTracking()
        .Where(organization => organization.Id == tenant.RequiredOrganizationId)
        .Select(organization => organization.RequireDeadlineReview)
        .SingleAsync(cancellationToken);
}

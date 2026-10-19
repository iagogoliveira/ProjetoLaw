using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Triage;

internal sealed class PublicationWorkflowAccess(PublicationAccess readable, ICurrentAccess current,
    ICaseDirectory cases, IOrganizationAccessDirectory members, IResourceAuthorizer<GetPublicationQuery> readAuthorization)
{
    public async Task<Result<Publication>> GetAsync(Guid id, string permission, CancellationToken cancellationToken, bool tracking = false)
    {
        var read = await readAuthorization.AuthorizeAsync(new GetPublicationQuery(id), cancellationToken);
        if (read.IsFailure) { return PublicationErrors.NotFound; }
        var query = await readable.VisibleAsync(cancellationToken);
        var publication = await (tracking ? query.AsTracking() : query).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (publication is null) { return PublicationErrors.NotFound; }
        var allowed = publication.LegalCaseId is { } caseId
            ? await cases.AuthorizeAsync(caseId, permission, cancellationToken)
            : (await current.ScopesForAsync(permission, cancellationToken)).Any(s => s.Scope == AccessScope.Organization)
                ? Result.Success : Result.Failure(PublicationErrors.NotFound);
        return allowed.IsFailure ? PublicationErrors.NotFound : publication;
    }

    public async Task<Result> LinkAllowedAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var read = await cases.AuthorizeAsync(caseId, PublicationAccess.Read, cancellationToken);
        if (read.IsFailure) { return PublicationErrors.NotFound; }
        var triage = await cases.AuthorizeAsync(caseId, "publications.triage", cancellationToken);
        return triage.IsFailure ? PublicationErrors.NotFound : Result.Success;
    }

    public async Task<bool> AssigneeAllowedAsync(Publication publication, Guid userId, Guid? caseId, CancellationToken cancellationToken)
    {
        var member = await members.GetAsync(userId, cancellationToken);
        if (member is null) { return false; }
        foreach (var permission in AssignmentPermissions)
        {
            if (!member.Permissions.TryGetValue(permission, out var grants)) { return false; }
            var scope = await cases.ScopeForUserAsync(userId, permission, cancellationToken);
            if (publication.CandidateCnjs.Any(c => scope.HiddenCnjs.Contains(c, StringComparer.Ordinal))) { return false; }
            if (caseId is { } linked ? !scope.VisibleIds.Contains(linked) : !grants.Any(g => g.Scope == OfficeAccessScope.Organization)) { return false; }
        }

        return true;
    }

    private static readonly string[] AssignmentPermissions = ["publications.read", "publications.triage"];
}

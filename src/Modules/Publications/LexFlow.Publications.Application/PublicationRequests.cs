using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.Publications.Application.Triage;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

public sealed record PublicationResponse(Guid Id, Guid SourceId, string Journal, DateOnly AvailableOn, DateOnly PublishedOn,
    string Text, string? ExtractedCnj, Guid? LegalCaseId, PublicationStatus Status, DateTimeOffset ReceivedAt)
{
    public Guid? AssigneeUserId { get; init; }
    public PublicationTriageResponse? Triage { get; init; }
}
public sealed record PublicationSummary(Guid Id, string Journal, DateOnly PublishedOn, string? ExtractedCnj,
    Guid? LegalCaseId, PublicationStatus Status, DateTimeOffset ReceivedAt)
{
    public Guid? AssigneeUserId { get; init; }
}
public sealed record PublicationPosition(DateTimeOffset ReceivedAt, Guid Id);
public sealed record PublicationsPage(IReadOnlyList<PublicationSummary> Items, PublicationPosition? Next);
public sealed record GetPublicationQuery(Guid Id) : IQuery<PublicationResponse>;
public sealed record ListPublicationsQuery(PublicationStatus? Status, bool? Linked, Guid? CaseId, PublicationPosition? After, int Limit,
    Guid? AssigneeUserId = null) : IQuery<PublicationsPage>;

internal sealed class ListPublicationsValidator : AbstractValidator<ListPublicationsQuery>
{
    public ListPublicationsValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}

internal sealed class PublicationAccess(IPublicationsDbContext db, ICaseDirectory cases, ICurrentAccess access)
{
    public const string Read = "publications.read";

    public async Task<IQueryable<Publication>> VisibleAsync(CancellationToken cancellationToken)
    {
        var ids = await cases.VisibleIdsAsync(Read, cancellationToken);
        var hiddenCnjs = await cases.InaccessibleCnjsAsync(Read, cancellationToken);
        var unlinked = (await access.ScopesForAsync(Read, cancellationToken)).Any(s => s.Scope == AccessScope.Organization);
        return db.Publications.AsNoTracking()
            .Where(p => !p.CandidateCnjs.Any(c => hiddenCnjs.Contains(c)))
            .Where(p => p.LegalCaseId != null ? ids.Contains(p.LegalCaseId.Value) : unlinked);
    }
}

internal sealed class GetPublicationHandler(PublicationAccess access, IPublicationsDbContext db) : IQueryHandler<GetPublicationQuery, PublicationResponse>
{
    public async Task<Result<PublicationResponse>> Handle(GetPublicationQuery query, CancellationToken cancellationToken)
    {
        var p = await (await access.VisibleAsync(cancellationToken)).SingleOrDefaultAsync(p => p.Id == query.Id, cancellationToken);
        if (p is null) { return PublicationErrors.NotFound; }
        var triage = await db.Triages.AsNoTracking().SingleOrDefaultAsync(t => t.PublicationId == p.Id, cancellationToken);
        return new PublicationResponse(p.Id, p.SourceId, p.Journal, p.AvailableOn,
            p.PublishedOn, p.Text, p.ExtractedCnj, p.LegalCaseId, p.Status, p.ReceivedAt)
        { AssigneeUserId = p.AssigneeUserId, Triage = triage is null ? null : PublicationTriageResponse.Of(triage) };
    }
}

internal sealed class GetPublicationAuthorizer(IPublicationsDbContext db, ICaseDirectory cases, ICurrentAccess access) : IResourceAuthorizer<GetPublicationQuery>
{
    public async Task<Result> AuthorizeAsync(GetPublicationQuery request, CancellationToken cancellationToken)
    {
        var publication = await db.Publications.AsNoTracking().Where(p => p.Id == request.Id)
            .Select(p => new { p.LegalCaseId }).SingleOrDefaultAsync(cancellationToken);
        if (publication is null) { return PublicationErrors.NotFound; }
        if (publication.LegalCaseId is { } caseId)
        {
            var result = await cases.AuthorizeAsync(caseId, PublicationAccess.Read, cancellationToken);
            return result.IsFailure ? PublicationErrors.NotFound : Result.Success;
        }

        return (await access.ScopesForAsync(PublicationAccess.Read, cancellationToken)).Any(s => s.Scope == AccessScope.Organization)
            ? Result.Success : PublicationErrors.NotFound;
    }
}
internal sealed class ListPublicationsHandler(PublicationAccess access) : IQueryHandler<ListPublicationsQuery, PublicationsPage>
{
    public async Task<Result<PublicationsPage>> Handle(ListPublicationsQuery query, CancellationToken cancellationToken)
    {
        var publications = await access.VisibleAsync(cancellationToken);
        if (query.Status is { } status) { publications = publications.Where(p => p.Status == status); }
        if (query.Linked is { } linked) { publications = publications.Where(p => (p.LegalCaseId != null) == linked); }
        if (query.CaseId is { } caseId) { publications = publications.Where(p => p.LegalCaseId == caseId); }
        if (query.AssigneeUserId is { } assignee) { publications = publications.Where(p => p.AssigneeUserId == assignee); }
        if (query.After is { } after)
        {
            publications = publications.Where(p => p.ReceivedAt < after.ReceivedAt || (p.ReceivedAt == after.ReceivedAt && p.Id.CompareTo(after.Id) < 0));
        }

        var page = await publications.OrderByDescending(p => p.ReceivedAt).ThenByDescending(p => p.Id).Take(query.Limit + 1)
            .Select(p => new PublicationSummary(p.Id, p.Journal, p.PublishedOn, p.ExtractedCnj, p.LegalCaseId, p.Status, p.ReceivedAt)
            { AssigneeUserId = p.AssigneeUserId })
            .ToListAsync(cancellationToken);
        var last = page.Count > query.Limit ? page[query.Limit - 1] : null;
        return new PublicationsPage(page.Take(query.Limit).ToList(), last is null ? null : new PublicationPosition(last.ReceivedAt, last.Id));
    }
}

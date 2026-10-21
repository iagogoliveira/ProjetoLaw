using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Control;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Infrastructure.Control;

internal sealed class PublicationControlReport(
    PublicationsDbContext db,
    ITenantContext tenant,
    IOrganizationAccessDirectory organizationAccess,
    ICaseDirectory cases) : IControlReportSource, IDailyDigestSource
{
    private const string PublicationsRead = "publications.read";
    private const string ControlRead = "schedule.control.read";

    public async Task<ControlSourceResult> ReadAsync(ControlWindow window, CancellationToken cancellationToken)
    {
        var member = await organizationAccess.GetAsync(window.UserId, cancellationToken);
        if (member is null || member.MfaRequired && !window.MultiFactorAuthenticated)
        {
            return EmptyControl();
        }

        var publicationScope = await cases.ScopeForUserAsync(window.UserId, PublicationsRead, cancellationToken);
        var controlScope = await cases.ScopeForUserAsync(window.UserId, ControlRead, cancellationToken);
        var visibleIds = publicationScope.VisibleIds.Intersect(controlScope.VisibleIds).ToArray();
        var hiddenCnjs = publicationScope.HiddenCnjs.Union(controlScope.HiddenCnjs, StringComparer.Ordinal).ToArray();
        var allowUnlinked = HasGrant(member, PublicationsRead, AccessScope.Organization)
            && HasGrant(member, ControlRead, AccessScope.Organization);
        var ids = await VisiblePublicationIdsAsync(visibleIds, hiddenCnjs, allowUnlinked, cancellationToken);
        return await ReadControlDataAsync(window, ids, cancellationToken);
    }

    public async Task<IReadOnlyList<DigestSection>> ReadAsync(Guid userId, DateOnly day, CancellationToken cancellationToken)
    {
        var member = await organizationAccess.GetAsync(userId, cancellationToken);
        if (member is null)
        {
            return [new DigestSection("publications", 0, [])];
        }

        var scope = await cases.ScopeForUserAsync(userId, PublicationsRead, cancellationToken);
        var allowUnlinked = HasGrant(member, PublicationsRead, AccessScope.Organization);
        var ids = await VisiblePublicationIdsAsync(scope.VisibleIds.ToArray(), scope.HiddenCnjs.ToArray(), allowUnlinked, cancellationToken);
        var pending = db.Publications.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Status == PublicationStatus.Pending);
        var count = await pending.LongCountAsync(cancellationToken);
        var items = await pending.OrderBy(p => p.ReceivedAt).ThenBy(p => p.Id).Take(20)
            .Select(p => new DigestItem("publication", p.Id, p.LegalCaseId, p.Journal, p.ReceivedAt))
            .ToListAsync(cancellationToken);
        return [new DigestSection("publications", count, items)];
    }

    private async Task<Guid[]> VisiblePublicationIdsAsync(IReadOnlyCollection<Guid> linkedCaseIds,
        IReadOnlyCollection<string> hiddenCnjs, bool allowUnlinked, CancellationToken cancellationToken)
    {
        return await db.Publications.AsNoTracking()
            .Where(p => !p.CandidateCnjs.Any(c => hiddenCnjs.Contains(c)))
            .Where(p => p.LegalCaseId != null
                ? linkedCaseIds.Contains(p.LegalCaseId.Value)
                : allowUnlinked)
            .Select(p => p.Id)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<ControlSourceResult> ReadControlDataAsync(ControlWindow window, Guid[] ids,
        CancellationToken cancellationToken)
    {
        const string countSql = """
            SELECT COUNT(*) AS "Count"
            FROM publications.publications
            WHERE organization_id = @OrganizationId AND id = ANY(@Ids) AND status = 'Pending'
            """;
        const string itemsSql = """
            SELECT id AS "Id", legal_case_id AS "CaseId", journal AS "Label", received_at AS "At"
            FROM publications.publications
            WHERE organization_id = @OrganizationId AND id = ANY(@Ids) AND status = 'Pending'
            ORDER BY received_at, id
            LIMIT @Limit
            """;
        var parameters = new { OrganizationId = tenant.OrganizationId, Ids = ids, window.Limit };
        var countRows = await TenantSql.QueryAsync<CountRow>(db, tenant, countSql, parameters, cancellationToken);
        var rows = await TenantSql.QueryAsync<ItemRow>(db, tenant, itemsSql, parameters, cancellationToken);
        var metrics = await ReadMetricsAsync(ids, window, cancellationToken);
        var count = countRows.Single().Count;
        var items = rows.Select(row => new ControlItem(row.Id, row.CaseId, row.Label, row.At)).ToArray();
        return new ControlSourceResult([new ControlSection("pendingPublications", count, items)], metrics);
    }

    private async Task<IReadOnlyList<ControlMetric>> ReadMetricsAsync(Guid[] ids, ControlWindow window,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*) AS "Samples",
                   AVG(EXTRACT(EPOCH FROM (t.triaged_at - p.received_at)) / 60) AS "AverageMinutes"
            FROM publications.publication_triages t
            INNER JOIN publications.publications p ON p.id = t.publication_id
            WHERE t.organization_id = @OrganizationId AND p.organization_id = @OrganizationId
              AND p.id = ANY(@Ids) AND t.triaged_at >= @From AND t.triaged_at < @Until
            """;
        var row = (await TenantSql.QueryAsync<MetricRow>(db, tenant, sql,
            new { OrganizationId = tenant.OrganizationId, Ids = ids, window.From, window.Until }, cancellationToken)).Single();
        return
        [
            new ControlMetric("averageTriageMinutes", row.AverageMinutes, "minutes", row.Samples),
            new ControlMetric("retriagedPublications", 0, "publications", row.Samples),
        ];
    }

    private static bool HasGrant(OfficeUserAccess member, string permission, AccessScope scope) =>
        member.Permissions.TryGetValue(permission, out var grants) && grants.Any(grant => grant.Scope == (OfficeAccessScope)scope);

    private static ControlSourceResult EmptyControl() => new(
        [new ControlSection("pendingPublications", 0, [])],
        [new ControlMetric("averageTriageMinutes", null, "minutes", 0), new ControlMetric("retriagedPublications", 0, "publications", 0)]);

    private sealed class CountRow
    {
        public long Count { get; init; }
    }

    private sealed class ItemRow
    {
        public Guid Id { get; init; }
        public Guid? CaseId { get; init; }
        public string Label { get; init; } = string.Empty;
        public DateTimeOffset At { get; init; }
    }

    private sealed class MetricRow
    {
        public long Samples { get; init; }
        public decimal? AverageMinutes { get; init; }
    }
}

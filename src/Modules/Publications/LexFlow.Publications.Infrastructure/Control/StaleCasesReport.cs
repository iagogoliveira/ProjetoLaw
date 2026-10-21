using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Control;
using LexFlow.Publications.Infrastructure.Persistence;

namespace LexFlow.Publications.Infrastructure.Control;

internal sealed class StaleCasesReport(
    PublicationsDbContext db,
    ITenantContext tenant,
    ICaseControlDirectory controlCases,
    IOrganizationAccessDirectory organizationAccess) : IControlReportSource
{
    public async Task<ControlSourceResult> ReadAsync(ControlWindow window, CancellationToken cancellationToken)
    {
        var member = await organizationAccess.GetAsync(window.UserId, cancellationToken);
        if (member is null || member.MfaRequired && !window.MultiFactorAuthenticated)
        {
            return Empty();
        }

        var active = await controlCases.ActiveAsync(window.UserId, window.MultiFactorAuthenticated, cancellationToken);
        if (active.Count == 0)
        {
            return Empty();
        }

        var caseIds = active.Select(c => c.Id).ToArray();
        const string sql = """
            SELECT legal_case_id AS "CaseId", MAX(occurred_at) AS "LastAt"
            FROM publications.case_movements
            WHERE organization_id = @OrganizationId AND legal_case_id = ANY(@CaseIds)
            GROUP BY legal_case_id
            """;
        var movementRows = await TenantSql.QueryAsync<MovementRow>(db, tenant, sql,
            new { OrganizationId = tenant.OrganizationId, CaseIds = caseIds }, cancellationToken);
        var lastByCase = movementRows.ToDictionary(row => row.CaseId, row => row.LastAt);
        var cutoff = window.AsOf.AddDays(-window.IdleDays);
        var stale = active
            .Select(c => new { Case = c, LastAt = lastByCase.GetValueOrDefault(c.Id, c.CreatedAt) })
            .Where(item => item.LastAt <= cutoff)
            .OrderBy(item => item.LastAt)
            .ThenBy(item => item.Case.Id)
            .ToArray();

        var items = stale.Take(window.Limit)
            .Select(item => new ControlItem(item.Case.Id, item.Case.Id, item.Case.Folder, item.LastAt))
            .ToArray();
        return new ControlSourceResult(
            [new ControlSection("staleCases", stale.LongLength, items)],
            []);
    }

    private static ControlSourceResult Empty() =>
        new([new ControlSection("staleCases", 0, [])], []);

    private sealed class MovementRow
    {
        public Guid CaseId { get; init; }
        public DateTimeOffset LastAt { get; init; }
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Control;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Infrastructure.Control;

internal sealed class SchedulingControlReport(
    SchedulingDbContext db,
    ITenantContext tenant,
    IOrganizationAccessDirectory members,
    ICaseDirectory cases,
    IOrganizationDirectory organization,
    IHearingDigest hearings) : IControlReportSource, IDailyDigestSource
{
    private const string CasesRead = "cases.read";
    private const string DeadlinesRead = "schedule.deadlines.read";
    private const string TasksRead = "schedule.tasks.read";
    private const string EventsRead = "schedule.events.read";
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<ControlSourceResult> ReadAsync(ControlWindow window, CancellationToken cancellationToken)
    {
        var member = await members.GetAsync(window.UserId, cancellationToken);
        if (member is null || member.MfaRequired && !window.MultiFactorAuthenticated)
        {
            return EmptyControl();
        }

        var caseIds = await AllowedControlCasesAsync(window.UserId, cancellationToken);
        if (caseIds.Count == 0) { return EmptyControl(); }

        const string sql = """
            SELECT id AS "Id", legal_case_id AS "CaseId", title AS "Title", status AS "Status",
                   fatal_due_on::timestamp AS "FatalDueOn", internal_due_on::timestamp AS "InternalDueOn",
                   assignee_user_id AS "AssigneeUserId", completion_evidence AS "CompletionEvidence",
                   confirmed_at AS "ConfirmedAt"
            FROM scheduling.deadlines
            WHERE organization_id = @OrganizationId AND legal_case_id = ANY(@CaseIds)
            """;
        var rows = await TenantSql.QueryAsync<DeadlineRow>(db, tenant, sql,
            new { OrganizationId = tenant.RequiredOrganizationId, CaseIds = caseIds.ToArray() }, cancellationToken);
        var activeUsers = (await organization.MemberUserIdsAsync(cancellationToken)).ToHashSet();
        var overdueOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(window.AsOf, SaoPaulo).DateTime);
        var unassigned = rows.Where(row => row.IsOpen && (row.AssigneeUserId == Guid.Empty || !activeUsers.Contains(row.AssigneeUserId)))
            .OrderBy(row => row.FatalDueOn).ThenBy(row => row.Id).ToArray();
        var withoutEvidence = rows.Where(row => row.IsOpen && DateOnly.FromDateTime(row.FatalDueOn) < overdueOn
                || row.IsCompleted && string.IsNullOrWhiteSpace(row.CompletionEvidence))
            .OrderBy(row => row.FatalDueOn).ThenBy(row => row.Id).ToArray();
        var confirmedInWindow = rows.Where(row => row.ConfirmedAt >= window.From && row.ConfirmedAt < window.Until).ToArray();
        var lateRegistered = confirmedInWindow.LongCount(row => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(row.ConfirmedAt, SaoPaulo).DateTime)
            > DateOnly.FromDateTime(row.InternalDueOn));

        return new ControlSourceResult(
            [
                Section("unassignedDeadlines", unassigned, window.Limit),
                Section("deadlinesWithoutEvidence", withoutEvidence, window.Limit),
            ],
            [new ControlMetric("lateRegisteredDeadlines", lateRegistered, "count", confirmedInWindow.LongLength)]);
    }

    public async Task<IReadOnlyList<DigestSection>> ReadAsync(Guid userId, DateOnly day, CancellationToken cancellationToken)
    {
        var member = await members.GetAsync(userId, cancellationToken);
        if (member is null) { return EmptyDigest(); }

        var deadlines = await ReadDeadlineDigestAsync(userId, day, cancellationToken);
        var agenda = await ReadAgendaDigestAsync(userId, day, member, cancellationToken);

        return
        [
            new DigestSection("agenda", agenda.Count, agenda.OrderBy(item => item.At).ThenBy(item => item.Id).Take(20).ToArray()),
            new DigestSection("deadlines", deadlines.Count, deadlines.OrderBy(item => item.At).ThenBy(item => item.Id).Take(20).ToArray()),
        ];
    }

    private async Task<List<DigestItem>> ReadDeadlineDigestAsync(Guid userId, DateOnly day, CancellationToken ct)
    {
        var allowedCases = await DigestCaseIdsAsync(userId, DeadlinesRead, ct);
        if (allowedCases.Count == 0) { return []; }
        var rows = await db.Deadlines.AsNoTracking()
            .Where(d => d.AssigneeUserId == userId && allowedCases.Contains(d.LegalCaseId)
                && d.Status != WorkItemStatus.Completed && d.Status != WorkItemStatus.Missed
                && d.FatalDueOn >= day && d.FatalDueOn < day.AddDays(7))
            .Select(d => new DeadlineDigestRow(d.Id, d.LegalCaseId, d.Title, d.FatalDueOn))
            .ToListAsync(ct);
        return rows.Select(d => new DigestItem("deadline", d.Id, d.CaseId, d.Title, LocalStart(d.DueOn))).ToList();
    }

    private async Task<List<DigestItem>> ReadAgendaDigestAsync(Guid userId, DateOnly day, OfficeUserAccess member, CancellationToken ct)
    {
        var items = await ReadTaskDigestAsync(userId, day, member, ct);
        items.AddRange(await ReadEventDigestAsync(userId, day, member, ct));
        var hearingItems = await hearings.ReadAsync(userId, LocalStart(day), LocalStart(day.AddDays(1)), ct);
        items.AddRange(hearingItems.Select(item => new DigestItem("hearing", item.Id, item.CaseId, "Audiência", item.ScheduledAt)));
        return items;
    }

    private async Task<List<DigestItem>> ReadTaskDigestAsync(Guid userId, DateOnly day, OfficeUserAccess member, CancellationToken ct)
    {
        var allowedCases = await DigestCaseIdsAsync(userId, TasksRead, ct);
        var administrative = await OwnScopeAllowedAsync(userId, TasksRead, member, ct);
        var rows = await db.Tasks.AsNoTracking()
            .Where(t => t.AssigneeUserId == userId && t.Status != TaskState.Completed && t.Status != TaskState.Cancelled
                && t.DueOn == day && (t.LegalCaseId == null && administrative
                    || t.LegalCaseId != null && allowedCases.Contains(t.LegalCaseId.Value)))
            .Select(t => new TaskDigestRow(t.Id, t.LegalCaseId, t.Title, t.DueOn))
            .ToListAsync(ct);
        return rows.Select(t => new DigestItem("task", t.Id, t.CaseId, t.Title, LocalStart(t.DueOn))).ToList();
    }

    private async Task<List<DigestItem>> ReadEventDigestAsync(Guid userId, DateOnly day, OfficeUserAccess member, CancellationToken ct)
    {
        var allowedCases = await DigestCaseIdsAsync(userId, EventsRead, ct);
        var administrative = await OwnScopeAllowedAsync(userId, EventsRead, member, ct);
        var dayStart = LocalStart(day);
        var dayEnd = LocalStart(day.AddDays(1));
        var rows = await db.Events.AsNoTracking()
            .Where(e => e.AssigneeUserId == userId && e.Status == CalendarEventState.Scheduled
                && e.StartsAt < dayEnd && e.EndsAt > dayStart
                && (e.LegalCaseId == null && administrative
                    || e.LegalCaseId != null && allowedCases.Contains(e.LegalCaseId.Value)))
            .Select(e => new EventDigestRow(e.Id, e.LegalCaseId, e.Title, e.StartsAt))
            .ToListAsync(ct);
        return rows.Select(e => new DigestItem("event", e.Id, e.CaseId, e.Title, e.StartsAt)).ToList();
    }

    private async Task<IReadOnlyCollection<Guid>> AllowedControlCasesAsync(Guid userId, CancellationToken ct)
    {
        var readable = await cases.ScopeForUserAsync(userId, CasesRead, ct);
        var deadlines = await cases.ScopeForUserAsync(userId, DeadlinesRead, ct);
        var control = await cases.ScopeForUserAsync(userId, ControlPermissions.Read, ct);
        return readable.VisibleIds.Intersect(deadlines.VisibleIds).Intersect(control.VisibleIds).ToArray();
    }

    private async Task<IReadOnlyCollection<Guid>> DigestCaseIdsAsync(Guid userId, string permission, CancellationToken ct)
    {
        var readable = await cases.ScopeForUserAsync(userId, CasesRead, ct);
        var permitted = await cases.ScopeForUserAsync(userId, permission, ct);
        return readable.VisibleIds.Intersect(permitted.VisibleIds).ToArray();
    }

    private async Task<bool> OwnScopeAllowedAsync(Guid userId, string permission, OfficeUserAccess member, CancellationToken ct)
    {
        if (!member.Permissions.TryGetValue(permission, out var grants)) { return false; }
        if (grants.Any(grant => grant.Scope is OfficeAccessScope.Own or OfficeAccessScope.Organization)) { return true; }
        var teams = grants.Where(grant => grant.Scope == OfficeAccessScope.Team && grant.ReferenceId is not null)
            .Select(grant => grant.ReferenceId!.Value).ToArray();
        return teams.Length > 0 && (await organization.UsersOfTeamsAsync(teams, ct)).Contains(userId);
    }

    private static DateTimeOffset LocalStart(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        while (SaoPaulo.IsInvalidTime(local)) { local = local.AddMinutes(1); }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, SaoPaulo));
    }

    private static ControlSection Section(string kind, IReadOnlyList<DeadlineRow> rows, int limit) =>
        new(kind, rows.Count, rows.Take(limit).Select(row => new ControlItem(row.Id, row.CaseId, row.Title,
            LocalStart(DateOnly.FromDateTime(row.FatalDueOn)))).ToArray());

    private static ControlSourceResult EmptyControl() => new(
        [new ControlSection("unassignedDeadlines", 0, []), new ControlSection("deadlinesWithoutEvidence", 0, [])],
        [new ControlMetric("lateRegisteredDeadlines", 0, "count", 0)]);

    private static IReadOnlyList<DigestSection> EmptyDigest() =>
        [new DigestSection("agenda", 0, []), new DigestSection("deadlines", 0, [])];

    private sealed class DeadlineRow
    {
        public Guid Id { get; init; }
        public Guid CaseId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime FatalDueOn { get; init; }
        public DateTime InternalDueOn { get; init; }
        public Guid AssigneeUserId { get; init; }
        public string? CompletionEvidence { get; init; }
        public DateTimeOffset ConfirmedAt { get; init; }
        public bool IsOpen => string.Equals(Status, nameof(WorkItemStatus.Pending), StringComparison.Ordinal)
            || string.Equals(Status, nameof(WorkItemStatus.InProgress), StringComparison.Ordinal);
        public bool IsCompleted => string.Equals(Status, nameof(WorkItemStatus.Completed), StringComparison.Ordinal);
    }

    private sealed record DeadlineDigestRow(Guid Id, Guid CaseId, string Title, DateOnly DueOn);
    private sealed record TaskDigestRow(Guid Id, Guid? CaseId, string Title, DateOnly DueOn);
    private sealed record EventDigestRow(Guid Id, Guid? CaseId, string Title, DateTimeOffset StartsAt);
}

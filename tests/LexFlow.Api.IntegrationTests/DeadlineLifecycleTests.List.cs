using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Organization.Domain.Roles;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class DeadlineLifecycleTests
{
    [Fact]
    public async Task Deadline_list_uses_half_open_window_and_pages_without_repeating_equal_due_dates()
    {
        var office = await OfficeAsync();
        var dates = await DatesAsync(office);
        var first = await RegisteredDeadlineAsync(office);
        var second = await RegisteredDeadlineAsync(office);
        var third = await RegisteredDeadlineAsync(office);

        var (firstStatus, firstPage) = await ListDeadlinesAsync(office.Admin, dates.FatalDueOn, AddDay(dates.FatalDueOn), limit: 1);
        firstStatus.ShouldBe(HttpStatusCode.OK, firstPage.ToString());
        firstPage.GetProperty("today").GetString().ShouldBe(_factory.Services.GetRequiredService<IClock>().Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        var ids = new List<Guid> { firstPage.GetProperty("items")[0].GetProperty("id").GetGuid() };
        var cursor = firstPage.GetProperty("nextCursor").GetString();
        cursor.ShouldNotBeNullOrWhiteSpace();

        while (cursor is not null)
        {
            var (status, page) = await ListDeadlinesAsync(office.Admin, dates.FatalDueOn, AddDay(dates.FatalDueOn), limit: 1, cursor: cursor);
            status.ShouldBe(HttpStatusCode.OK, page.ToString());
            ids.Add(page.GetProperty("items")[0].GetProperty("id").GetGuid());
            cursor = page.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : page.GetProperty("nextCursor").GetString();
        }

        ids.Count.ShouldBe(3);
        ids.Distinct().Count().ShouldBe(3);
        ids.Order().ShouldBe(new[] { first, second, third }.Order());

        var (excludedStatus, excluded) = await ListDeadlinesAsync(office.Admin, AddDay(dates.FatalDueOn), AddDay(AddDay(dates.FatalDueOn)));
        excludedStatus.ShouldBe(HttpStatusCode.OK, excluded.ToString());
        excluded.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Deadline_list_filters_by_case_assignee_and_closed_status()
    {
        var office = await OfficeAsync();
        var id = await RegisteredDeadlineAsync(office);
        var completed = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/deadlines/{id}/complete", new { evidence = "protocol 456" });
        completed.Status.ShouldBe(HttpStatusCode.OK, completed.Body.ToString());
        var dates = await DatesAsync(office);

        var (openStatus, open) = await ListDeadlinesAsync(office.Admin, dates.FatalDueOn, AddDay(dates.FatalDueOn), caseId: office.CaseId);
        openStatus.ShouldBe(HttpStatusCode.OK, open.ToString());
        open.GetProperty("items").GetArrayLength().ShouldBe(0);

        var (closedStatus, closed) = await ListDeadlinesAsync(office.Admin, dates.FatalDueOn, AddDay(dates.FatalDueOn),
            caseId: office.CaseId, assigneeUserId: office.Admin.UserId, includeClosed: true);
        closedStatus.ShouldBe(HttpStatusCode.OK, closed.ToString());
        var item = closed.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(id);
        item.GetProperty("caseId").GetGuid().ShouldBe(office.CaseId);
        item.GetProperty("assigneeUserId").GetGuid().ShouldBe(office.Admin.UserId);
        item.GetProperty("status").GetString().ShouldBe("completed");
        item.TryGetProperty("requireReview", out _).ShouldBeTrue();
        item.TryGetProperty("reviewedAt", out _).ShouldBeTrue();

        var (teamStatus, teamFiltered) = await ListDeadlinesAsync(office.Admin, dates.FatalDueOn, AddDay(dates.FatalDueOn),
            teamId: Guid.CreateVersion7());
        teamStatus.ShouldBe(HttpStatusCode.OK, teamFiltered.ToString());
        teamFiltered.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Deadline_list_rejects_invalid_window_limit_and_cursor()
    {
        var office = await OfficeAsync();
        var invalid = new[]
        {
            "/api/v1/deadlines?from=2027-03-02&until=2027-03-01",
            "/api/v1/deadlines?from=2027-01-01&until=2028-01-03",
            "/api/v1/deadlines?from=2027-03-01&until=2027-03-02&limit=0",
            "/api/v1/deadlines?from=2027-03-01&until=2027-03-02&limit=101",
            "/api/v1/deadlines?from=2027-03-01&until=2027-03-02&cursor=not-base64",
        };

        foreach (var path in invalid)
        {
            var (status, body) = await SendAsync(office.Admin, HttpMethod.Get, path);
            status.ShouldBe(HttpStatusCode.BadRequest, $"{path}: {body}");
            OrganizationApi.Code(body).ShouldBe("scheduling.deadline_list_invalid");
        }
    }

    [Fact]
    public async Task Deadline_list_obeys_case_scope_confidential_restrictions_and_tenant_isolation()
    {
        var office = await OfficeAsync();
        var dates = await DatesAsync(office);
        await RegisteredDeadlineAsync(office);
        var scopedOut = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "cases.read", "schedule.deadlines.read");
        var reader = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Organization, null,
            "cases.read", "schedule.deadlines.read");
        var restricted = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Organization, null,
            "cases.read", "schedule.deadlines.read");
        var restriction = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/cases/{office.CaseId}/restrictions",
            new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = restricted.UserId } } });
        restriction.Status.ShouldBe(HttpStatusCode.OK, restriction.Body.ToString());
        var path = $"/api/v1/deadlines?from={dates.FatalDueOn}&until={AddDay(dates.FatalDueOn)}&caseId={office.CaseId}";

        var (readerStatus, readable) = await SendAsync(reader, HttpMethod.Get, path);
        var (scopedStatus, scoped) = await SendAsync(scopedOut, HttpMethod.Get, path);
        var (restrictedStatus, confidential) = await SendAsync(restricted, HttpMethod.Get, path);
        readerStatus.ShouldBe(HttpStatusCode.OK, readable.ToString());
        readable.GetProperty("items").GetArrayLength().ShouldBe(1);
        scopedStatus.ShouldBe(HttpStatusCode.OK, scoped.ToString());
        scoped.GetProperty("items").GetArrayLength().ShouldBe(0);
        restrictedStatus.ShouldBe(HttpStatusCode.OK, confidential.ToString());
        confidential.GetProperty("items").GetArrayLength().ShouldBe(0);

        var otherOffice = await OfficeAsync();
        var (tenantStatus, tenant) = await SendAsync(otherOffice.Admin, HttpMethod.Get, path);
        tenantStatus.ShouldBe(HttpStatusCode.OK, tenant.ToString());
        tenant.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> ListDeadlinesAsync(SeededMember member, string from, string until,
        int? limit = null, string? cursor = null, bool? includeClosed = null, Guid? assigneeUserId = null, Guid? teamId = null, Guid? caseId = null)
    {
        var query = $"from={from}&until={until}";
        if (limit is { } take) { query += $"&limit={take}"; }
        if (cursor is not null) { query += $"&cursor={Uri.EscapeDataString(cursor)}"; }
        if (includeClosed is { } closed) { query += $"&includeClosed={closed.ToString().ToLowerInvariant()}"; }
        if (assigneeUserId is { } assignee) { query += $"&assigneeUserId={assignee}"; }
        if (teamId is { } team) { query += $"&teamId={team}"; }
        if (caseId is { } scopedCase) { query += $"&caseId={scopedCase}"; }
        return SendAsync(member, HttpMethod.Get, $"/api/v1/deadlines?{query}");
    }

    private static string AddDay(string value) => DateOnly.Parse(value, System.Globalization.CultureInfo.InvariantCulture).AddDays(1)
        .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

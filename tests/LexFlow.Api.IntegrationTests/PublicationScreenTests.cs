using System.Net;
using LexFlow.Organization.Domain.Roles;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class PublicationWorkflowTests
{
    [Fact]
    public async Task Pending_counter_uses_visible_pending_rows_and_isolates_tenants()
    {
        var office = await OfficeAsync();
        var other = await OfficeAsync();
        var source = await SourceAsync(office);
        var first = await ReceiveAsync(office, source);
        await ReceiveAsync(office, source);
        await ReceiveAsync(other, await SourceAsync(other));
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/pending-count")).Body.GetProperty("count").GetInt32().ShouldBe(2);
        (await TriageAsync(office, first, Discard())).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/pending-count")).Body.GetProperty("count").GetInt32().ShouldBe(1);
        var limited = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null, "publications.read");
        (await SendAsync(limited, HttpMethod.Get, "/api/v1/publications/pending-count")).Body.GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Workflow_case_choices_follow_triage_scope_without_settings_permissions()
    {
        var office = await OfficeAsync();
        var limited = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "publications.read", "publications.triage");
        var visible = await CaseAsync(office, limited.UserId);
        await CaseAsync(office);
        var result = await SendAsync(limited, HttpMethod.Get, "/api/v1/publications/workflow-options");
        result.Status.ShouldBe(HttpStatusCode.OK);
        result.Body.GetProperty("cases").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(visible);
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "publications.read");
        (await SendAsync(reader, HttpMethod.Get, "/api/v1/publications/workflow-options")).Body.GetProperty("cases").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Workflow_choices_validate_search_length()
    {
        var office = await OfficeAsync();
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/workflow-options?q=" + new string('x', 201))).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/workflow-options?q=missing")).Body.GetProperty("cases").GetArrayLength().ShouldBe(0);
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, cnj: cnj);
        var options = await SendAsync(office, HttpMethod.Get, "/api/v1/publications/workflow-options?q=" + cnj);
        options.Body.GetProperty("cases").EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(caseId);
    }

    [Fact]
    public async Task Workflow_members_are_only_current_office_members_without_email_or_settings_access()
    {
        var office = await OfficeAsync();
        var other = await OfficeAsync();
        var included = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail(), fullName: "Visible member");
        var excluded = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail(), fullName: "Other office");
        await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, included.Id, "publications.read");
        await TestOrganizations.AddMemberAsync(dependencies, other.OrganizationId, excluded.Id, "publications.read");
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "publications.read");
        var result = await SendAsync(reader, HttpMethod.Get, "/api/v1/publications/workflow-options");
        result.Status.ShouldBe(HttpStatusCode.OK);
        var member = result.Body.GetProperty("members").EnumerateArray().Single();
        member.GetProperty("userId").GetGuid().ShouldBe(included.Id);
        member.GetProperty("name").GetString().ShouldBe("Visible member");
        member.TryGetProperty("email", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Counter_and_case_choices_hide_blocked_cases_and_ambiguous_candidates()
    {
        var office = await OfficeAsync();
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "publications.read", "publications.triage");
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, cnj: cnj);
        await ReceiveAsync(office, await SourceAsync(office), "Texto " + cnj);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions",
            new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = reader.UserId } } })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(reader, HttpMethod.Get, "/api/v1/publications/pending-count")).Body.GetProperty("count").GetInt32().ShouldBe(0);
        (await SendAsync(reader, HttpMethod.Get, "/api/v1/publications/workflow-options")).Body.GetProperty("cases").GetArrayLength().ShouldBe(0);
    }
}

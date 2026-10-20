using System.Net;
using LexFlow.Organization.Domain.Roles;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class DeadlineLifecycleTests
{
    [Fact]
    public async Task Deadline_endpoints_enforce_read_manage_verify_and_tenant_access()
    {
        var owner = await OfficeAsync();
        var dates = await DatesAsync(owner);
        var created = await CreateDeadlineAsync(owner, Request(owner.Admin, dates));
        var id = created.Body.GetProperty("id").GetGuid();
        var reader = await TestOrganizations.AddScopedMemberAsync(_dependencies, owner.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Organization, null,
            "cases.read", "schedule.deadlines.read");
        var managerWithoutRead = await TestOrganizations.AddScopedMemberAsync(_dependencies, owner.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Organization, null,
            "cases.read", "schedule.deadlines.manage");
        var managerWithoutVerify = await TestOrganizations.AddScopedMemberAsync(_dependencies, owner.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Organization, null,
            "cases.read", "schedule.deadlines.read", "schedule.deadlines.manage");
        var otherOffice = await OfficeAsync();

        (await SendAsync(reader, HttpMethod.Get, $"/api/v1/deadlines/{id}")).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(reader, HttpMethod.Post, $"/api/v1/deadlines/{id}/start")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(managerWithoutRead, HttpMethod.Get, $"/api/v1/deadlines/{id}")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(managerWithoutVerify, HttpMethod.Post, $"/api/v1/deadlines/{id}/review")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(otherOffice.Admin, HttpMethod.Get, $"/api/v1/deadlines/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(otherOffice.Admin, HttpMethod.Post, $"/api/v1/deadlines/{id}/start")).Status.ShouldBe(HttpStatusCode.NotFound);

        var (settingsGet, _) = await SendAsync(reader, HttpMethod.Get, "/api/v1/organization/deadline-review");
        var (settingsPut, _) = await SendAsync(reader, HttpMethod.Put, "/api/v1/organization/deadline-review", new { requireDeadlineReview = true });
        settingsGet.ShouldBe(HttpStatusCode.Forbidden);
        settingsPut.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deadline_read_obeys_case_scope_and_confidential_restrictions()
    {
        var office = await OfficeAsync();
        var dates = await DatesAsync(office);
        var created = await CreateDeadlineAsync(office, Request(office.Admin, dates));
        var id = created.Body.GetProperty("id").GetGuid();
        var scopedOut = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "cases.read", "schedule.deadlines.read", "schedule.deadlines.manage");
        var restricted = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Organization, null,
            "cases.read", "schedule.deadlines.read", "schedule.deadlines.manage");
        var restriction = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/cases/{office.CaseId}/restrictions",
            new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = restricted.UserId } } });

        restriction.Status.ShouldBe(HttpStatusCode.OK, restriction.Body.ToString());
        (await SendAsync(scopedOut, HttpMethod.Get, $"/api/v1/deadlines/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(restricted, HttpMethod.Get, $"/api/v1/deadlines/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(restricted, HttpMethod.Post, $"/api/v1/deadlines/{id}/start")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Manual_deadline_rejects_an_assignee_who_cannot_access_the_case()
    {
        var office = await OfficeAsync();
        var dates = await DatesAsync(office);
        var outOfScope = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "cases.read", "schedule.deadlines.read");

        var (status, body) = await CreateDeadlineAsync(office, Request(office.Admin, dates, assignee: outOfScope.UserId));

        status.ShouldBe(HttpStatusCode.BadRequest, body.ToString());
        OrganizationApi.Code(body).ShouldBe("scheduling.assignee_unavailable");
    }

    [Fact]
    public async Task Review_requires_another_member_except_in_a_solo_office()
    {
        var office = await OfficeAsync();
        await SendAsync(office.Admin, HttpMethod.Put, "/api/v1/organization/deadline-review", new { requireDeadlineReview = true });
        var dates = await DatesAsync(office);
        var created = await CreateDeadlineAsync(office, Request(office.Admin, dates));
        var id = created.Body.GetProperty("id").GetGuid();
        var peer = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Organization, null,
            "cases.read", "schedule.deadlines.read", "schedule.deadlines.verify");

        var selfReview = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/deadlines/{id}/review");
        var peerReview = await SendAsync(peer, HttpMethod.Post, $"/api/v1/deadlines/{id}/review");
        selfReview.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(selfReview.Body).ShouldBe("scheduling.separate_reviewer_required");
        peerReview.Status.ShouldBe(HttpStatusCode.OK, peerReview.Body.ToString());
        peerReview.Body.GetProperty("reviewedBy").GetGuid().ShouldBe(peer.UserId);

        var soloOffice = await OfficeAsync();
        await SendAsync(soloOffice.Admin, HttpMethod.Put, "/api/v1/organization/deadline-review", new { requireDeadlineReview = true });
        var soloDates = await DatesAsync(soloOffice);
        var solo = await CreateDeadlineAsync(soloOffice, Request(soloOffice.Admin, soloDates));
        var soloId = solo.Body.GetProperty("id").GetGuid();
        var soloReview = await SendAsync(soloOffice.Admin, HttpMethod.Post, $"/api/v1/deadlines/{soloId}/review");
        soloReview.Status.ShouldBe(HttpStatusCode.OK, soloReview.Body.ToString());
        soloReview.Body.GetProperty("reviewedBy").GetGuid().ShouldBe(soloOffice.Admin.UserId);
    }

    [Fact]
    public async Task Timeline_contains_deadline_activities_only_when_the_member_can_read_deadlines()
    {
        var office = await OfficeAsync();
        var deadline = await RegisteredDeadlineAsync(office);
        await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/deadlines/{deadline}/start");
        var memberWithoutScheduleRead = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7(), DataScope.Organization, null,
            "cases.read");

        var (_, visibleTimeline) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/cases/{office.CaseId}/timeline?kind=scheduling.deadline_started");
        var (_, hiddenTimeline) = await SendAsync(memberWithoutScheduleRead, HttpMethod.Get, $"/api/v1/cases/{office.CaseId}/timeline?kind=scheduling.deadline_started");

        visibleTimeline.GetProperty("items").GetArrayLength().ShouldBe(1);
        visibleTimeline.GetProperty("items")[0].GetProperty("kind").GetString().ShouldBe("scheduling.deadline_started");
        visibleTimeline.GetProperty("items")[0].GetProperty("data").GetProperty("actorUserId").GetString().ShouldBe(office.Admin.UserId.ToString());
        hiddenTimeline.GetProperty("items").GetArrayLength().ShouldBe(0);
    }
}

using System.Net;
using System.Text.Json;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class PublicationWorkflowTests
{
    [Fact]
    public async Task Distribution_requires_active_triager_with_case_scope_and_can_be_cleared()
    {
        var office = await OfficeAsync();
        var assignee = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "publications.read", "publications.triage", "cases.read");
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, assignee.UserId, cnj);
        var publication = await ReceiveAsync(office, await SourceAsync(office), "Texto " + cnj);
        var assigned = await SendAsync(office, HttpMethod.Put, $"/api/v1/publications/{publication}/assignment", new { assigneeUserId = assignee.UserId });
        assigned.Status.ShouldBe(HttpStatusCode.OK, assigned.Body.ToString());
        var list = await SendAsync(assignee, HttpMethod.Get, $"/api/v1/publications?assignee={assignee.UserId}");
        list.Status.ShouldBe(HttpStatusCode.OK);
        list.Body.GetProperty("items").EnumerateArray().Single().GetProperty("assigneeUserId").GetGuid().ShouldBe(assignee.UserId);
        var detail = await SendAsync(office, HttpMethod.Get, $"/api/v1/publications/{publication}");
        detail.Body.GetProperty("assigneeUserId").GetGuid().ShouldBe(assignee.UserId);
        var cleared = await SendAsync(office, HttpMethod.Put, $"/api/v1/publications/{publication}/assignment", new { assigneeUserId = (Guid?)null });
        cleared.Status.ShouldBe(HttpStatusCode.OK);
        cleared.Body.GetProperty("assigneeUserId").ValueKind.ShouldBe(JsonValueKind.Null);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/tasks")).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Assignment_rejects_unknown_non_triager_and_inactive_member()
    {
        var office = await OfficeAsync();
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "publications.read");
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publications/{publication}/assignment", new { assigneeUserId = Guid.CreateVersion7() })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publications/{publication}/assignment", new { assigneeUserId = reader.UserId })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var active = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "publications.read", "publications.triage");
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<OrganizationDbContext>();
            db.Members.Remove(await db.Members.SingleAsync(m => m.Id == active.MemberId, Ct));
            await db.SaveChangesAsync(Ct);
            await services.GetRequiredService<IAccessCache>().InvalidateOrganizationAsync(office.OrganizationId, Ct);
        });
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publications/{publication}/assignment", new { assigneeUserId = active.UserId })).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Manual_link_is_audited_and_cannot_be_changed_after_triage()
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publications/{publication}/link", new { caseId })).Status.ShouldBe(HttpStatusCode.OK);
        var done = await TriageAsync(office, publication, new { classification = "routine", action = "noAction", confirmed = true });
        done.Status.ShouldBe(HttpStatusCode.OK, done.Body.ToString());
        done.Body.GetProperty("triage").GetProperty("deadlineId").ValueKind.ShouldBe(JsonValueKind.Null);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publications/{publication}/link", new { caseId })).Status.ShouldBe(HttpStatusCode.Conflict);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publications/{publication}/assignment", new { assigneeUserId = office.UserId })).Status.ShouldBe(HttpStatusCode.Conflict);
    }
}

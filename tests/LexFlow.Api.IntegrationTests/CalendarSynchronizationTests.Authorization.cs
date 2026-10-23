using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Infrastructure.Persistence;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class CalendarSynchronizationTests
{
    private static readonly string[] CasePermissions = ["identity.profile.read", "identity.profile.manage", "schedule.events.read", "schedule.events.manage", "cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create", "schedule.deadlines.read", "schedule.deadlines.manage", "schedule.deadlines.verify"];
    private static readonly string[] ClientRoles = ["client"];

    private async Task<Guid> CaseAsync(SeededMember owner)
    {
        var client = await _api.CreateAsync("/api/v1/people", owner.UserId, owner.OrganizationId, new { kind = "company", name = "Calendar private client", roles = ClientRoles });
        return await _api.CreateAsync("/api/v1/cases", owner.UserId, owner.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = owner.UserId } },
        });
    }

    private async Task<JsonElement> DeadlineAsync(SeededMember owner, Guid caseId)
    {
        var (_, calculation) = await SendAsync(owner, HttpMethod.Post, "/api/v1/deadlines/calculate", new
        { caseId, startKind = "publication", triggerDate = "2027-04-01", days = 5, mode = "businessDays", internalLeadDays = 1 });
        var result = await SendAsync(owner, HttpMethod.Post, $"/api/v1/cases/{caseId}/deadlines", new
        {
            type = "appeal",
            title = "Confidential appeal deadline",
            assigneeUserId = owner.UserId,
            rule = new { startKind = "publication", triggerDate = "2027-04-01", days = 5, mode = "businessDays", doubled = false, internalLeadDays = 1 },
            confirmedInternalDueOn = calculation.GetProperty("internalDueOn").GetString(),
            confirmedFatalDueOn = calculation.GetProperty("fatalDueOn").GetString(),
            confirmed = true,
        });
        result.Status.ShouldBe(HttpStatusCode.Created, result.Body.ToString());
        return result.Body;
    }

    private async Task ReplacePermissionsAsync(SeededMember owner, string[] permissions)
    {
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "calendar-permission-test");
        await using var provider = _dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var role = await db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Id == owner.RoleId, TestContext.Current.CancellationToken);
        role.SetPermissions(permissions);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<IAccessCache>().InvalidateOrganizationAsync(owner.OrganizationId, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("complete", "completionEvidence", "Proof kept private")]
    [InlineData("miss", "missedReason", "Reason kept private")]
    public async Task Deadlines_only_export_and_remote_changes_never_modify_dates_review_or_evidence(string transition, string evidenceField, string evidence)
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, CasePermissions);
        var connection = await ConnectAsync(owner);
        var deadline = await DeadlineAsync(owner, await CaseAsync(owner));
        var id = deadline.GetProperty("id").GetGuid();
        (await SendAsync(owner, HttpMethod.Post, $"/api/v1/deadlines/{id}/review")).Status.ShouldBe(HttpStatusCode.OK);
        var before = await SendAsync(owner, HttpMethod.Get, $"/api/v1/deadlines/{id}");
        await SyncAsync(owner, connection);
        var remote = _provider.Snapshot(Context(owner)).Single();
        remote.Day.ShouldBe(DateOnly.Parse(deadline.GetProperty("fatalDueOn").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        remote.Title.ShouldBe("Prazo LexFlow");
        remote.Description.ShouldBeNull();
        remote.Location.ShouldBeNull();
        _provider.Seed(Context(owner), [remote with { Version = "2", Day = remote.Day!.Value.AddDays(5), StartsAt = remote.StartsAt.AddDays(5), EndsAt = remote.EndsAt.AddDays(5), IsCancelled = true, Title = "External deadline title" }]);
        await SyncAsync(owner, connection);
        var after = await SendAsync(owner, HttpMethod.Get, $"/api/v1/deadlines/{id}");
        foreach (var field in new[] { "fatalDueOn", "internalDueOn", "reviewedAt", "reviewedBy", "completionEvidence", "missedReason", "title", "status" })
        { after.Body.GetProperty(field).ToString().ShouldBe(before.Body.GetProperty(field).ToString()); }
        var restored = _provider.Snapshot(Context(owner)).Single();
        restored.Day.ShouldBe(remote.Day);
        restored.StartsAt.ShouldBe(remote.StartsAt);
        restored.IsCancelled.ShouldBeFalse();
        restored.Title.ShouldBe("External deadline title");
        var closed = await SendAsync(owner, HttpMethod.Post, $"/api/v1/deadlines/{id}/{transition}", transition == "complete" ? new { evidence } : (object)new { reason = evidence });
        closed.Status.ShouldBe(HttpStatusCode.OK, closed.Body.ToString());
        await SyncAsync(owner, connection);
        _provider.Snapshot(Context(owner)).Single().IsCancelled.ShouldBeTrue();
        var final = await SendAsync(owner, HttpMethod.Get, $"/api/v1/deadlines/{id}");
        final.Body.GetProperty(evidenceField).GetString().ShouldBe(evidence);
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Pending_decision_is_owned_by_user_and_requires_current_manage_permission()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        await SyncAsync(owner, connection);
        _provider.Seed(Context(owner), []);
        await SyncAsync(owner, connection);
        var pending = (await PendingAsync(owner, connection)).EnumerateArray().Single().GetProperty("id").GetGuid();
        var colleague = await TestOrganizations.AddMemberAsync(_dependencies, owner.OrganizationId, Guid.CreateVersion7(), Permissions);
        var outsider = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        foreach (var stranger in new[] { colleague, outsider })
        {
            (await ResolveAsync(stranger, connection, pending, "acceptExternal")).Status.ShouldBe(HttpStatusCode.NotFound);
            (await SendAsync(stranger, HttpMethod.Get, $"/api/v1/me/calendar-connections/{connection}/pending")).Status.ShouldBe(HttpStatusCode.NotFound);
        }
        await ReplacePermissionsAsync(owner, ["identity.profile.read", "identity.profile.manage", "schedule.events.read"]);
        var denied = await ResolveAsync(owner, connection, pending, "acceptExternal");
        denied.Status.ShouldBe(HttpStatusCode.NotFound, denied.Body.ToString());
        (await ReadEventAsync(owner, id)).GetProperty("status").GetString().ShouldBe("scheduled");
        (await ResolveAsync(owner, connection, pending, "keepLocal")).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Permission_revocation_hides_pending_and_prevents_exporting_local_changes()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        await SyncAsync(owner, connection);
        var remote = _provider.Snapshot(Context(owner)).Single();
        _provider.Seed(Context(owner), [remote with { Version = "2", IsCancelled = true }]);
        await SyncAsync(owner, connection);
        await EventAsync(owner, id, hours: 3);
        await ReplacePermissionsAsync(owner, ["identity.profile.read", "identity.profile.manage"]);
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
        await SyncAsync(owner, connection);
        _provider.Snapshot(Context(owner)).Single().StartsAt.ShouldBe(Start);
    }

    [Fact]
    public async Task Sigilo_revocation_hides_deadline_and_event_pending_and_prevents_current_exports()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, CasePermissions);
        var supervisor = await TestOrganizations.AddMemberAsync(_dependencies, owner.OrganizationId, Guid.CreateVersion7(), CasePermissions);
        var connection = await ConnectAsync(owner);
        var caseId = await CaseAsync(owner);
        await DeadlineAsync(owner, caseId);
        var created = await SendAsync(owner, HttpMethod.Post, "/api/v1/schedule/events", new
        { caseId, title = "Secret case meeting", startsAt = Start, endsAt = Start.AddHours(1), assigneeUserId = owner.UserId });
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        await SyncAsync(owner, connection);
        var initial = _provider.Snapshot(Context(owner));
        _provider.Seed(Context(owner), initial.Select(r => r with { Version = "2", IsCancelled = true }).ToArray());
        await SyncAsync(owner, connection);
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(1);
        var restriction = await SendAsync(supervisor, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions", new
        { allowed = new[] { new { userId = supervisor.UserId } }, blocked = new[] { new { userId = owner.UserId } } });
        restriction.Status.ShouldBe(HttpStatusCode.OK, restriction.Body.ToString());
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
        var prior = _provider.Snapshot(Context(owner)).ToArray();
        await SyncAsync(owner, connection);
        _provider.Snapshot(Context(owner)).ShouldBe(prior);
    }

    [Fact]
    public async Task Unavailable_provider_records_stable_error_preserves_local_and_disconnected_queued_job_has_no_effect()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        _options.Available = false;
        await SyncAsync(owner, connection);
        (await ReadEventAsync(owner, id)).GetProperty("title").GetString().ShouldBe("Private title");
        using (var ambient = AmbientContext.Begin(owner.OrganizationId, "calendar-error-test"))
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(owner.OrganizationId);
            var saved = await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().CalendarConnections.SingleAsync(c => c.Id == connection, TestContext.Current.CancellationToken);
            saved.LastErrorCode.ShouldBe("scheduling.calendar_provider_unavailable");
        }
        _options.Available = true;
        (await SendAsync(owner, HttpMethod.Post, $"/api/v1/me/calendar-connections/{connection}/sync")).Status.ShouldBe(HttpStatusCode.Accepted);
        (await SendAsync(owner, HttpMethod.Post, $"/api/v1/me/calendar-connections/{connection}/disconnect")).Status.ShouldBe(HttpStatusCode.OK);
        await _factory.RunJobsAsync(owner.OrganizationId);
        _provider.Snapshot(Context(owner)).Count.ShouldBe(0);
        (await ReadEventAsync(owner, id)).GetProperty("title").GetString().ShouldBe("Private title");
    }
}

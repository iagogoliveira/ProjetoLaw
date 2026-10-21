using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class SchedulingRedistributionTests : IAsyncLifetime
{
    private static readonly string[] AdminPermissions =
    [
        "cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create",
        "schedule.deadlines.read", "schedule.deadlines.manage", "schedule.deadlines.verify",
        "schedule.tasks.read", "schedule.tasks.manage", "schedule.events.read", "schedule.events.manage",
        "settings.organization.manage", "settings.structure.manage", "settings.custom_fields.read",
    ];
    private static readonly string[] ClientRole = ["client"];
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private readonly DependenciesFixture _dependencies;
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public SchedulingRedistributionTests(DependenciesFixture dependencies) => _dependencies = dependencies;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(_dependencies.Settings);
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record Office(SeededMember Admin, Guid CaseId, Guid ClosingReasonId);

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    private async Task AssertAssigneeAsync(SeededMember reader, Guid taskId, Guid expectedAssignee)
    {
        var (status, task) = await SendAsync(reader, HttpMethod.Get, $"/api/v1/tasks/{taskId}");
        status.ShouldBe(HttpStatusCode.OK, task.ToString());
        task.GetProperty("assigneeUserId").GetGuid().ShouldBe(expectedAssignee);
    }

    private static void AssertBatchItem(JsonElement item, bool changed, string? errorCode, bool hidden = false)
    {
        item.GetProperty("changed").GetBoolean().ShouldBe(changed);
        if (errorCode is null) { item.GetProperty("errorCode").ValueKind.ShouldBe(JsonValueKind.Null); }
        else { item.GetProperty("errorCode").GetString().ShouldBe(errorCode); }
        if (hidden) { item.TryGetProperty("title", out _).ShouldBeFalse(); }
    }

    private async Task<Office> OfficeAsync()
    {
        var admin = await TestOrganizations.CreateWithMemberAsync(_dependencies, AdminPermissions);
        using (AmbientContext.Begin(admin.OrganizationId, "scheduling-redistribution-tests"))
        await using (var provider = _dependencies.PlatformServices())
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LookupTableSeeder>().SeedAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().SaveChangesAsync(Ct);
        }

        var clientId = await _api.CreateAsync("/api/v1/people", admin.UserId, admin.OrganizationId,
            new { kind = "company", name = $"Cliente {Guid.CreateVersion7():N}", roles = ClientRole });
        var caseId = await CreateCaseAsync(admin, admin.UserId, clientId);
        var (_, tables) = await SendAsync(admin, HttpMethod.Get, "/api/v1/lookup-tables");
        var closingReasonId = tables.EnumerateArray()
            .Single(t => t.GetProperty("systemKey").GetString() == SystemLookupTables.ClosingReasons)
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        return new Office(admin, caseId, closingReasonId);
    }

    private async Task<Guid> CreateCaseAsync(SeededMember admin, Guid lawyer, Guid? clientId = null)
    {
        clientId ??= await _api.CreateAsync("/api/v1/people", admin.UserId, admin.OrganizationId,
            new { kind = "company", name = $"Cliente {Guid.CreateVersion7():N}", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", admin.UserId, admin.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = clientId.Value, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = lawyer } },
        });
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> CreateTaskAsync(SeededMember actor, Guid organizationId,
        Guid assignee, Guid? caseId = null, string title = "Revisar petição") =>
        _api.SendAsync(HttpMethod.Post, "/api/v1/tasks", actor.UserId, organizationId, new
        {
            caseId,
            title,
            description = "Descrição de tarefa",
            dueOn = "2027-04-05",
            assigneeUserId = assignee,
            priority = "normal",
        });

    private Task<(HttpStatusCode Status, JsonElement Body)> CreateEventAsync(Office office, Guid assignee, string title = "Reunião") =>
        SendAsync(office.Admin, HttpMethod.Post, "/api/v1/schedule/events", new
        {
            caseId = (Guid?)null,
            title,
            startsAt = "2027-04-05T13:00:00Z",
            endsAt = "2027-04-05T14:00:00Z",
            assigneeUserId = assignee,
        });

    private async Task<(Guid Id, JsonElement Body)> CreateDeadlineAsync(Office office)
    {
        var (calculationStatus, calculation) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/deadlines/calculate", new
        {
            caseId = office.CaseId,
            startKind = "publication",
            triggerDate = "2027-04-01",
            days = 5,
            mode = "businessDays",
            internalLeadDays = 1,
        });
        calculationStatus.ShouldBe(HttpStatusCode.OK, calculation.ToString());
        var (status, body) = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/cases/{office.CaseId}/deadlines", new
        {
            type = "appeal",
            title = $"Prazo {Guid.CreateVersion7():N}",
            assigneeUserId = office.Admin.UserId,
            rule = new
            {
                startKind = "publication",
                triggerDate = "2027-04-01",
                consultedOn = (string?)null,
                days = 5,
                mode = "businessDays",
                doubled = false,
                internalLeadDays = 1,
            },
            confirmedInternalDueOn = calculation.GetProperty("internalDueOn").GetString(),
            confirmedFatalDueOn = calculation.GetProperty("fatalDueOn").GetString(),
            confirmed = true,
        });
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        return (body.GetProperty("id").GetGuid(), body);
    }

    [Fact]
    public async Task Redistribution_routes_validate_batch_bounds_and_unique_ids()
    {
        var office = await OfficeAsync();
        var body = new { ids = Array.Empty<Guid>(), assigneeUserId = office.Admin.UserId };
        foreach (var path in new[]
        {
            "/api/v1/tasks/redistribute",
            "/api/v1/deadlines/redistribute",
            "/api/v1/schedule/events/redistribute",
        })
        {
            var (emptyStatus, empty) = await SendAsync(office.Admin, HttpMethod.Post, path, body);
            var duplicateId = Guid.CreateVersion7();
            var duplicate = await SendAsync(office.Admin, HttpMethod.Post, path,
                new { ids = new[] { duplicateId, duplicateId }, assigneeUserId = office.Admin.UserId });
            var tooMany = await SendAsync(office.Admin, HttpMethod.Post, path,
                new { ids = Enumerable.Range(0, 1001).Select(_ => Guid.CreateVersion7()).ToArray(), assigneeUserId = office.Admin.UserId });

            emptyStatus.ShouldBe(HttpStatusCode.BadRequest, empty.ToString());
            OrganizationApi.Code(empty).ShouldBe("scheduling.batch_invalid");
            duplicate.Status.ShouldBe(HttpStatusCode.BadRequest, duplicate.Body.ToString());
            OrganizationApi.Code(duplicate.Body).ShouldBe("scheduling.batch_invalid");
            tooMany.Status.ShouldBe(HttpStatusCode.BadRequest, tooMany.Body.ToString());
            OrganizationApi.Code(tooMany.Body).ShouldBe("scheduling.batch_invalid");
        }
    }

    [Fact]
    public async Task Task_redistribution_partially_persists_and_hides_closed_hidden_and_foreign_items()
    {
        var office = await OfficeAsync();
        var requester = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Organization, null, "cases.read", "schedule.tasks.read", "schedule.tasks.manage");
        var destination = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Organization, null, "cases.read", "schedule.tasks.read");
        var foreignOffice = await TestOrganizations.CreateWithMemberAsync(_dependencies, "schedule.tasks.read", "schedule.tasks.manage");

        var valid = await CreateTaskAsync(office.Admin, office.Admin.OrganizationId, requester.UserId, office.CaseId, "Atribuída ao solicitante");
        var hiddenCaseId = await CreateCaseAsync(office.Admin, office.Admin.UserId);
        var restriction = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/cases/{hiddenCaseId}/restrictions",
            new { allowed = new[] { new { userId = office.Admin.UserId } }, blocked = new[] { new { userId = requester.UserId } } });
        restriction.Status.ShouldBe(HttpStatusCode.OK, restriction.Body.ToString());
        var hidden = await CreateTaskAsync(office.Admin, office.Admin.OrganizationId, office.Admin.UserId, hiddenCaseId, "Caso restrito");
        var closed = await CreateTaskAsync(office.Admin, office.Admin.OrganizationId, requester.UserId, office.CaseId, "Concluída");
        var foreign = await CreateTaskAsync(foreignOffice, foreignOffice.OrganizationId, foreignOffice.UserId, title: "Outra organização");
        valid.Status.ShouldBe(HttpStatusCode.Created, valid.Body.ToString());
        hidden.Status.ShouldBe(HttpStatusCode.Created, hidden.Body.ToString());
        closed.Status.ShouldBe(HttpStatusCode.Created, closed.Body.ToString());
        foreign.Status.ShouldBe(HttpStatusCode.Created, foreign.Body.ToString());
        var validId = valid.Body.GetProperty("id").GetGuid();
        var hiddenId = hidden.Body.GetProperty("id").GetGuid();
        var closedId = closed.Body.GetProperty("id").GetGuid();
        var foreignId = foreign.Body.GetProperty("id").GetGuid();
        (await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{closedId}/state", new { status = "completed" })).Status.ShouldBe(HttpStatusCode.OK);

        var (status, result) = await SendAsync(requester, HttpMethod.Post, "/api/v1/tasks/redistribute",
            new { ids = new[] { validId, closedId, hiddenId, foreignId }, assigneeUserId = destination.UserId });
        var items = result.GetProperty("items").EnumerateArray().ToArray();

        status.ShouldBe(HttpStatusCode.OK, result.ToString());
        items.Length.ShouldBe(4);
        AssertBatchItem(items[0], true, null);
        AssertBatchItem(items[1], false, "scheduling.task_transition_invalid");
        AssertBatchItem(items[2], false, "scheduling.task_not_found", hidden: true);
        AssertBatchItem(items[3], false, "scheduling.task_not_found");
        await AssertAssigneeAsync(office.Admin, validId, destination.UserId);
        await AssertAssigneeAsync(office.Admin, closedId, requester.UserId);
        await AssertAssigneeAsync(office.Admin, hiddenId, office.Admin.UserId);
    }

    [Fact]
    public async Task Task_redistribution_rechecks_current_case_scope_before_mutating()
    {
        var office = await OfficeAsync();
        var requester = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Organization, null, "cases.read", "schedule.tasks.read", "schedule.tasks.manage");
        var destination = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Organization, null, "cases.read", "schedule.tasks.read");
        var caseId = await CreateCaseAsync(office.Admin, requester.UserId);
        var task = await CreateTaskAsync(office.Admin, office.Admin.OrganizationId, requester.UserId, caseId, "Acesso revogado");
        var taskId = task.Body.GetProperty("id").GetGuid();
        var visible = await SendAsync(requester, HttpMethod.Get, $"/api/v1/tasks?caseId={caseId}");
        var restriction = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions",
            new { allowed = new[] { new { userId = office.Admin.UserId } }, blocked = new[] { new { userId = requester.UserId } } });

        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        visible.Status.ShouldBe(HttpStatusCode.OK, visible.Body.ToString());
        visible.Body.GetProperty("items").EnumerateArray().ShouldContain(item => item.GetProperty("id").GetGuid() == taskId);
        restriction.Status.ShouldBe(HttpStatusCode.OK, restriction.Body.ToString());
        var (status, result) = await SendAsync(requester, HttpMethod.Post, "/api/v1/tasks/redistribute",
            new { ids = new[] { taskId }, assigneeUserId = destination.UserId });
        var (_, after) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/tasks/{taskId}");

        status.ShouldBe(HttpStatusCode.OK, result.ToString());
        result.GetProperty("items")[0].GetProperty("changed").GetBoolean().ShouldBeFalse();
        result.GetProperty("items")[0].GetProperty("errorCode").GetString().ShouldBe("scheduling.task_not_found");
        result.GetProperty("items")[0].TryGetProperty("title", out _).ShouldBeFalse();
        after.GetProperty("assigneeUserId").GetGuid().ShouldBe(requester.UserId);
    }

    [Fact]
    public async Task Task_redistribution_rejects_closed_case_and_leaves_assignee_unchanged()
    {
        var office = await OfficeAsync();
        var destination = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Organization, null, "cases.read", "schedule.tasks.read");
        var caseId = await CreateCaseAsync(office.Admin, office.Admin.UserId);
        var task = await CreateTaskAsync(office.Admin, office.Admin.OrganizationId, office.Admin.UserId, caseId, "Processo encerrado");
        var taskId = task.Body.GetProperty("id").GetGuid();
        var close = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/cases/{caseId}/close",
            new { reasonId = office.ClosingReasonId, outcome = "denied", closedOn = "2027-04-01" });
        var (status, result) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/tasks/redistribute",
            new { ids = new[] { taskId }, assigneeUserId = destination.UserId });
        var (_, after) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/tasks/{taskId}");

        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        close.Status.ShouldBe(HttpStatusCode.OK, close.Body.ToString());
        status.ShouldBe(HttpStatusCode.OK, result.ToString());
        result.GetProperty("items")[0].GetProperty("changed").GetBoolean().ShouldBeFalse();
        result.GetProperty("items")[0].GetProperty("errorCode").GetString().ShouldBe("scheduling.case_unavailable");
        after.GetProperty("assigneeUserId").GetGuid().ShouldBe(office.Admin.UserId);
    }

    [Fact]
    public async Task Administrative_task_redistribution_requires_the_recipient_to_be_in_its_team_scope()
    {
        var office = await OfficeAsync();
        var teamId = await _api.CreateAsync("/api/v1/organization/teams", office.Admin.UserId, office.Admin.OrganizationId,
            new { name = $"Equipe {Guid.CreateVersion7():N}" });
        var outside = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Team, teamId, "schedule.tasks.read");
        var inside = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Team, teamId, "schedule.tasks.read");
        var membership = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/organization/teams/{teamId}/members",
            new { memberIds = new[] { inside.MemberId } });
        var task = await CreateTaskAsync(office.Admin, office.Admin.OrganizationId, office.Admin.UserId, title: "Tarefa administrativa");
        var taskId = task.Body.GetProperty("id").GetGuid();
        var (outsideStatus, outsideResult) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/tasks/redistribute",
            new { ids = new[] { taskId }, assigneeUserId = outside.UserId });
        var (_, unchanged) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/tasks/{taskId}");
        var (insideStatus, insideResult) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/tasks/redistribute",
            new { ids = new[] { taskId }, assigneeUserId = inside.UserId });
        var (_, reassigned) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/tasks/{taskId}");

        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        membership.Status.ShouldBe(HttpStatusCode.OK, membership.Body.ToString());
        outsideStatus.ShouldBe(HttpStatusCode.OK, outsideResult.ToString());
        AssertBatchItem(outsideResult.GetProperty("items")[0], false, "scheduling.assignee_unavailable");
        unchanged.GetProperty("assigneeUserId").GetGuid().ShouldBe(office.Admin.UserId);
        insideStatus.ShouldBe(HttpStatusCode.OK, insideResult.ToString());
        AssertBatchItem(insideResult.GetProperty("items")[0], true, null);
        reassigned.GetProperty("assigneeUserId").GetGuid().ShouldBe(inside.UserId);
    }

    [Fact]
    public async Task Event_redistribution_keeps_cancelled_items_and_unreadable_recipients_unchanged()
    {
        var office = await OfficeAsync();
        var destination = await TestOrganizations.AddMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), "schedule.events.read");
        var withoutRead = await TestOrganizations.AddMemberAsync(_dependencies, office.Admin.OrganizationId, Guid.CreateVersion7());
        var active = await CreateEventAsync(office, office.Admin.UserId, "Ativa");
        var cancelled = await CreateEventAsync(office, office.Admin.UserId, "Cancelada");
        var activeId = active.Body.GetProperty("id").GetGuid();
        var cancelledId = cancelled.Body.GetProperty("id").GetGuid();
        (await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/schedule/events/{cancelledId}/cancel")).Status.ShouldBe(HttpStatusCode.OK);

        var (partialStatus, partial) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/schedule/events/redistribute",
            new { ids = new[] { activeId, cancelledId }, assigneeUserId = destination.UserId });
        var (invalidStatus, invalid) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/schedule/events/redistribute",
            new { ids = new[] { activeId }, assigneeUserId = withoutRead.UserId });
        var (_, activeAfter) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/schedule/events/{activeId}");
        var (_, cancelledAfter) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/schedule/events/{cancelledId}");

        partialStatus.ShouldBe(HttpStatusCode.OK, partial.ToString());
        partial.GetProperty("items")[0].GetProperty("changed").GetBoolean().ShouldBeTrue();
        partial.GetProperty("items")[1].GetProperty("changed").GetBoolean().ShouldBeFalse();
        partial.GetProperty("items")[1].GetProperty("errorCode").GetString().ShouldBe("scheduling.event_closed");
        invalidStatus.ShouldBe(HttpStatusCode.OK, invalid.ToString());
        invalid.GetProperty("items")[0].GetProperty("changed").GetBoolean().ShouldBeFalse();
        invalid.GetProperty("items")[0].GetProperty("errorCode").GetString().ShouldBe("scheduling.assignee_unavailable");
        activeAfter.GetProperty("assigneeUserId").GetGuid().ShouldBe(destination.UserId);
        cancelledAfter.GetProperty("assigneeUserId").GetGuid().ShouldBe(office.Admin.UserId);
    }

    [Fact]
    public async Task Deadline_redistribution_preserves_dates_review_and_adds_audit_activity()
    {
        var office = await OfficeAsync();
        var destination = await TestOrganizations.AddMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), "cases.read", "schedule.deadlines.read");
        var reviewer = await TestOrganizations.AddMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), "cases.read", "schedule.deadlines.verify");
        (await SendAsync(office.Admin, HttpMethod.Put, "/api/v1/organization/deadline-review", new { requireDeadlineReview = true }))
            .Status.ShouldBe(HttpStatusCode.OK);
        var deadline = await CreateDeadlineAsync(office);
        var id = deadline.Id;
        var review = await SendAsync(reviewer, HttpMethod.Post, $"/api/v1/deadlines/{id}/review");
        review.Status.ShouldBe(HttpStatusCode.OK, review.Body.ToString());
        var (beforeStatus, before) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/deadlines/{id}");
        beforeStatus.ShouldBe(HttpStatusCode.OK, before.ToString());

        var (status, body) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/deadlines/redistribute",
            new { ids = new[] { id }, assigneeUserId = destination.UserId });
        var (readStatus, after) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/deadlines/{id}");
        List<DeadlineActivity> activities;
        using (AmbientContext.Begin(office.Admin.OrganizationId, "scheduling-redistribution-tests"))
        await using (var scope = _factory.Services.CreateAsyncScope())
        using (scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(office.Admin.OrganizationId))
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            activities = await db.DeadlineActivities.Where(activity => activity.DeadlineId == id).ToListAsync(Ct);
        }

        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("items")[0].GetProperty("changed").GetBoolean().ShouldBeTrue();
        readStatus.ShouldBe(HttpStatusCode.OK, after.ToString());
        after.GetProperty("assigneeUserId").GetGuid().ShouldBe(destination.UserId);
        after.GetProperty("startOn").GetString().ShouldBe(before.GetProperty("startOn").GetString());
        after.GetProperty("internalDueOn").GetString().ShouldBe(before.GetProperty("internalDueOn").GetString());
        after.GetProperty("fatalDueOn").GetString().ShouldBe(before.GetProperty("fatalDueOn").GetString());
        after.GetProperty("reviewedBy").GetGuid().ShouldBe(before.GetProperty("reviewedBy").GetGuid());
        after.GetProperty("reviewedAt").GetDateTimeOffset().ShouldBe(before.GetProperty("reviewedAt").GetDateTimeOffset());
        activities.ShouldContain(a => a.DeadlineId == id && a.Kind == "scheduling.deadline_reassigned" && a.ActorUserId == office.Admin.UserId);
    }
}

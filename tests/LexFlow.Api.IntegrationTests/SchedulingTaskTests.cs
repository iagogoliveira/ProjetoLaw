using System.Net;
using System.Text.Json;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class SchedulingTaskTests : IAsyncLifetime
{
    private static readonly string[] AdminPermissions = ["cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create", "schedule.tasks.read", "schedule.tasks.manage", "settings.structure.manage"];
    private static readonly string[] ClientRole = ["client"];
    private readonly DependenciesFixture _dependencies;
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public SchedulingTaskTests(DependenciesFixture dependencies) => _dependencies = dependencies;

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

    private sealed record Office(SeededMember Admin, Guid CaseId, Guid ClientId);

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    private async Task<Office> OfficeAsync()
    {
        var admin = await TestOrganizations.CreateWithMemberAsync(_dependencies, AdminPermissions);
        var client = await _api.CreateAsync("/api/v1/people", admin.UserId, admin.OrganizationId,
            new { kind = "company", name = $"Cliente {Guid.CreateVersion7():N}", roles = ClientRole });
        var caseId = await _api.CreateAsync("/api/v1/cases", admin.UserId, admin.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = admin.UserId } },
        });
        return new Office(admin, caseId, client);
    }

    private static object TaskInput(Guid? caseId, Guid assignee, string title = "Prepare filing", string priority = "normal") => new
    {
        caseId,
        title,
        description = "Supporting details",
        dueOn = "2027-03-15",
        assigneeUserId = assignee,
        priority,
    };

    private Task<(HttpStatusCode Status, JsonElement Body)> CreateTaskAsync(Office office, string title = "Prepare filing", string priority = "normal") =>
        SendAsync(office.Admin, HttpMethod.Post, "/api/v1/tasks", TaskInput(office.CaseId, office.Admin.UserId, title, priority));

    [Fact]
    public async Task Tasks_can_be_created_read_and_edited_with_lifecycle_metadata()
    {
        var office = await OfficeAsync();
        var (createdStatus, created) = await CreateTaskAsync(office);
        var id = created.GetProperty("id").GetGuid();
        var (getStatus, detail) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/tasks/{id}");
        var (editStatus, edited) = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/tasks/{id}",
            TaskInput(office.CaseId, office.Admin.UserId, "  Review filing  ", "urgent"));

        createdStatus.ShouldBe(HttpStatusCode.Created, created.ToString());
        created.GetProperty("status").GetString().ShouldBe("pending");
        created.GetProperty("priority").GetString().ShouldBe("normal");
        created.GetProperty("caseId").GetGuid().ShouldBe(office.CaseId);
        getStatus.ShouldBe(HttpStatusCode.OK, detail.ToString());
        detail.GetProperty("id").GetGuid().ShouldBe(id);
        editStatus.ShouldBe(HttpStatusCode.OK, edited.ToString());
        edited.GetProperty("title").GetString().ShouldBe("Review filing");
        edited.GetProperty("priority").GetString().ShouldBe("urgent");
    }

    [Fact]
    public async Task Checklist_and_active_subtasks_block_completion_until_resolved_and_cancelled_child_releases_parent()
    {
        var office = await OfficeAsync();
        var task = await CreateTaskAsync(office);
        var taskId = task.Body.GetProperty("id").GetGuid();
        var checklist = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/checklist", new { title = "Verify attachments" });
        var checklistId = checklist.Body.GetProperty("checklist")[0].GetProperty("id").GetGuid();
        var subtask = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/subtasks", TaskInput(office.CaseId, office.Admin.UserId, "Gather exhibits"));
        var subtaskId = subtask.Body.GetProperty("id").GetGuid();

        var blockedByChecklist = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/state", new { status = "completed" });
        var checklistDone = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/tasks/{taskId}/checklist/{checklistId}", new { completed = true });
        var blockedBySubtask = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/state", new { status = "completed" });
        var cancelChild = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{subtaskId}/state", new { status = "cancelled" });
        var completeParent = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/state", new { status = "completed" });
        var reopen = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/state", new { status = "pending" });

        checklist.Status.ShouldBe(HttpStatusCode.OK, checklist.Body.ToString());
        subtask.Status.ShouldBe(HttpStatusCode.Created, subtask.Body.ToString());
        OrganizationApi.Code(blockedByChecklist.Body).ShouldBe("scheduling.task_unresolved");
        checklistDone.Status.ShouldBe(HttpStatusCode.OK, checklistDone.Body.ToString());
        OrganizationApi.Code(blockedBySubtask.Body).ShouldBe("scheduling.task_unresolved");
        cancelChild.Status.ShouldBe(HttpStatusCode.OK, cancelChild.Body.ToString());
        cancelChild.Body.GetProperty("status").GetString().ShouldBe("cancelled");
        completeParent.Status.ShouldBe(HttpStatusCode.OK, completeParent.Body.ToString());
        completeParent.Body.GetProperty("status").GetString().ShouldBe("completed");
        reopen.Status.ShouldBe(HttpStatusCode.OK, reopen.Body.ToString());
        reopen.Body.GetProperty("status").GetString().ShouldBe("pending");
    }

    [Fact]
    public async Task Task_comments_are_trimmed_and_legacy_case_list_keeps_pending_status()
    {
        var office = await OfficeAsync();
        var task = await CreateTaskAsync(office);
        var taskId = task.Body.GetProperty("id").GetGuid();
        var comment = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/comments", new { text = "  Awaiting client documents  " });
        var complete = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/state", new { status = "completed" });
        var legacy = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/cases/{office.CaseId}/tasks");

        comment.Status.ShouldBe(HttpStatusCode.OK, comment.Body.ToString());
        comment.Body.GetProperty("comments")[0].GetProperty("text").GetString().ShouldBe("Awaiting client documents");
        comment.Body.GetProperty("comments")[0].GetProperty("authorUserId").GetGuid().ShouldBe(office.Admin.UserId);
        complete.Status.ShouldBe(HttpStatusCode.OK, complete.Body.ToString());
        legacy.Status.ShouldBe(HttpStatusCode.OK, legacy.Body.ToString());
        var item = legacy.Body.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == taskId);
        item.GetProperty("status").GetString().ShouldBe("pending");
        item.GetProperty("lifecycleStatus").GetString().ShouldBe("completed");
    }

    [Fact]
    public async Task Task_list_filters_by_case_status_and_priority_and_uses_cursor_pagination()
    {
        var office = await OfficeAsync();
        var first = (await CreateTaskAsync(office, "First", "low")).Body.GetProperty("id").GetGuid();
        var second = (await CreateTaskAsync(office, "Second", "high")).Body.GetProperty("id").GetGuid();
        var third = (await CreateTaskAsync(office, "Third", "urgent")).Body.GetProperty("id").GetGuid();
        await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{second}/state", new { status = "inProgress" });

        var (pageOneStatus, pageOne) = await SendAsync(office.Admin, HttpMethod.Get, "/api/v1/tasks?limit=1");
        var cursor = pageOne.GetProperty("nextCursor").GetGuid();
        var (pageTwoStatus, pageTwo) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/tasks?limit=1&after={cursor}");
        var (filteredStatus, filtered) = await SendAsync(office.Admin, HttpMethod.Get,
            $"/api/v1/tasks?caseId={office.CaseId}&status=inProgress&priority=high");

        pageOneStatus.ShouldBe(HttpStatusCode.OK, pageOne.ToString());
        pageOne.GetProperty("items").GetArrayLength().ShouldBe(1);
        pageOne.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(first);
        cursor.ShouldBe(first);
        pageTwoStatus.ShouldBe(HttpStatusCode.OK, pageTwo.ToString());
        pageTwo.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(second);
        filteredStatus.ShouldBe(HttpStatusCode.OK, filtered.ToString());
        filtered.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldBe([second]);
        third.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Task_creation_requires_an_assignee_with_read_access_to_the_case()
    {
        var office = await OfficeAsync();
        var unreadableAssignee = await TestOrganizations.AddMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), "schedule.tasks.read");

        var (status, problem) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/tasks",
            TaskInput(office.CaseId, unreadableAssignee.UserId));

        status.ShouldBe(HttpStatusCode.BadRequest, problem.ToString());
        OrganizationApi.Code(problem).ShouldBe("scheduling.assignee_unavailable");
    }

    [Fact]
    public async Task Restricted_cases_hide_their_tasks_from_members_outside_the_access_list()
    {
        var office = await OfficeAsync();
        var blocked = await TestOrganizations.AddMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), "cases.read", "schedule.tasks.read");
        var restricted = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/cases/{office.CaseId}/restrictions",
            new { allowed = new[] { new { userId = office.Admin.UserId } }, blocked = new[] { new { userId = blocked.UserId } } });
        var task = await CreateTaskAsync(office);
        var taskId = task.Body.GetProperty("id").GetGuid();

        var (status, hidden) = await SendAsync(blocked, HttpMethod.Get, $"/api/v1/tasks/{taskId}");

        restricted.Status.ShouldBe(HttpStatusCode.OK, restricted.Body.ToString());
        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        status.ShouldBe(HttpStatusCode.NotFound, hidden.ToString());
        OrganizationApi.Code(hidden).ShouldBe("scheduling.task_not_found");
    }

    [Fact]
    public async Task Administrative_tasks_can_be_filtered_by_assignee_and_team()
    {
        var office = await OfficeAsync();
        var colleague = await TestOrganizations.AddMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), "schedule.tasks.read");
        var teamId = await _api.CreateAsync("/api/v1/organization/teams", office.Admin.UserId, office.Admin.OrganizationId,
            new { name = $"Equipe {Guid.CreateVersion7():N}" });
        (await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/organization/teams/{teamId}/members", new { memberIds = new[] { colleague.MemberId } }))
            .Status.ShouldBe(HttpStatusCode.OK);
        var adminTask = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/tasks", TaskInput(null, office.Admin.UserId, "Office review"));
        var colleagueTask = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/tasks", TaskInput(null, colleague.UserId, "Team review"));

        var (teamStatus, teamTasks) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/tasks?teamId={teamId}");
        var (assigneeStatus, assigneeTasks) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/tasks?assigneeUserId={colleague.UserId}");

        adminTask.Status.ShouldBe(HttpStatusCode.Created, adminTask.Body.ToString());
        colleagueTask.Status.ShouldBe(HttpStatusCode.Created, colleagueTask.Body.ToString());
        teamStatus.ShouldBe(HttpStatusCode.OK, teamTasks.ToString());
        teamTasks.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())
            .ShouldBe([colleagueTask.Body.GetProperty("id").GetGuid()]);
        assigneeStatus.ShouldBe(HttpStatusCode.OK, assigneeTasks.ToString());
        assigneeTasks.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())
            .ShouldBe([colleagueTask.Body.GetProperty("id").GetGuid()]);
    }
}

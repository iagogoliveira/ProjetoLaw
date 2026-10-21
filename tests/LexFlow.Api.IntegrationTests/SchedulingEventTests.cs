using System.Globalization;
using System.Net;
using System.Text.Json;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class SchedulingEventTests : IAsyncLifetime
{
    private static readonly string[] AdminPermissions = ["cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create", "schedule.events.read", "schedule.events.manage"];
    private static readonly string[] ClientRole = ["client"];
    private readonly DependenciesFixture _dependencies;
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public SchedulingEventTests(DependenciesFixture dependencies) => _dependencies = dependencies;

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

    private sealed record Office(SeededMember Admin, Guid CaseId);

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
        return new Office(admin, caseId);
    }

    private static object EventInput(Guid? caseId, Guid assignee, string title = "Client meeting",
        string startsAt = "2027-04-05T10:00:00-03:00", string endsAt = "2027-04-05T11:00:00-03:00") => new
        {
            caseId,
            title,
            description = "Review the next steps",
            startsAt,
            endsAt,
            assigneeUserId = assignee,
            location = "Room 2",
        };

    [Fact]
    public async Task Events_can_be_created_read_edited_and_cancelled_and_dates_are_normalized_to_utc()
    {
        var office = await OfficeAsync();
        const string path = "/api/v1/schedule/events";
        var (createdStatus, created) = await SendAsync(office.Admin, HttpMethod.Post, path, EventInput(office.CaseId, office.Admin.UserId));
        var id = created.GetProperty("id").GetGuid();
        var (getStatus, detail) = await SendAsync(office.Admin, HttpMethod.Get, $"{path}/{id}");
        var edited = await SendAsync(office.Admin, HttpMethod.Put, $"{path}/{id}", EventInput(office.CaseId, office.Admin.UserId, "  Client meeting moved  ",
            "2027-04-05T14:00:00Z", "2027-04-05T15:30:00Z"));
        var cancelled = await SendAsync(office.Admin, HttpMethod.Post, $"{path}/{id}/cancel");
        var editClosed = await SendAsync(office.Admin, HttpMethod.Put, $"{path}/{id}", EventInput(office.CaseId, office.Admin.UserId, "Changed after cancellation"));
        var cancelAgain = await SendAsync(office.Admin, HttpMethod.Post, $"{path}/{id}/cancel");

        createdStatus.ShouldBe(HttpStatusCode.Created, created.ToString());
        created.GetProperty("startsAt").GetDateTimeOffset().Offset.ShouldBe(TimeSpan.Zero);
        created.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(DateTimeOffset.Parse("2027-04-05T13:00:00Z", CultureInfo.InvariantCulture));
        getStatus.ShouldBe(HttpStatusCode.OK, detail.ToString());
        detail.GetProperty("caseId").GetGuid().ShouldBe(office.CaseId);
        edited.Status.ShouldBe(HttpStatusCode.OK, edited.Body.ToString());
        edited.Body.GetProperty("title").GetString().ShouldBe("Client meeting moved");
        edited.Body.GetProperty("endsAt").GetDateTimeOffset().ShouldBe(DateTimeOffset.Parse("2027-04-05T15:30:00Z", CultureInfo.InvariantCulture));
        cancelled.Status.ShouldBe(HttpStatusCode.OK, cancelled.Body.ToString());
        cancelled.Body.GetProperty("status").GetString().ShouldBe("cancelled");
        editClosed.Status.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(editClosed.Body).ShouldBe("scheduling.event_closed");
        cancelAgain.Status.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Events_reject_empty_titles_and_nonpositive_intervals()
    {
        var office = await OfficeAsync();
        const string path = "/api/v1/schedule/events";

        var inverted = await SendAsync(office.Admin, HttpMethod.Post, path,
            EventInput(office.CaseId, office.Admin.UserId, startsAt: "2027-04-05T11:00:00Z", endsAt: "2027-04-05T10:00:00Z"));
        var emptyTitle = await SendAsync(office.Admin, HttpMethod.Post, path,
            EventInput(office.CaseId, office.Admin.UserId, title: " "));

        inverted.Status.ShouldBe(HttpStatusCode.BadRequest, inverted.Body.ToString());
        OrganizationApi.Code(inverted.Body).ShouldBe("scheduling.event_invalid");
        emptyTitle.Status.ShouldBe(HttpStatusCode.BadRequest, emptyTitle.Body.ToString());
        OrganizationApi.Code(emptyTitle.Body).ShouldBe("scheduling.event_invalid");
    }

    [Fact]
    public async Task Event_access_is_limited_to_members_who_can_read_the_case_and_assignee()
    {
        var office = await OfficeAsync();
        const string path = "/api/v1/schedule/events";
        var eventId = (await SendAsync(office.Admin, HttpMethod.Post, path, EventInput(office.CaseId, office.Admin.UserId)))
            .Body.GetProperty("id").GetGuid();
        var scopedReader = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), LexFlow.Organization.Domain.Roles.DataScope.Own, null, "cases.read", "schedule.events.read");
        var unreadableAssignee = await TestOrganizations.AddMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), "schedule.events.read");

        var (hiddenStatus, hidden) = await SendAsync(scopedReader, HttpMethod.Get, $"{path}/{eventId}");
        var (invalidAssigneeStatus, invalidAssignee) = await SendAsync(office.Admin, HttpMethod.Post, path,
            EventInput(office.CaseId, unreadableAssignee.UserId));

        hiddenStatus.ShouldBe(HttpStatusCode.NotFound, hidden.ToString());
        OrganizationApi.Code(hidden).ShouldBe("scheduling.event_not_found");
        invalidAssigneeStatus.ShouldBe(HttpStatusCode.BadRequest, invalidAssignee.ToString());
        OrganizationApi.Code(invalidAssignee).ShouldBe("scheduling.assignee_unavailable");
    }
}

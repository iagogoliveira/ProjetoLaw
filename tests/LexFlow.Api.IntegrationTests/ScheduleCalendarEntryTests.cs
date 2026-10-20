using System.Net;
using System.Text.Json;
using LexFlow.Platform.Contracts.Courts;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class ScheduleCalendarEntryTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private const string Path = "/api/v1/schedule/calendar-entries";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] ControllerPermissions = ["schedule.deadlines.read", "schedule.calendar.manage"];
    private static readonly string[] LawyerPermissions = ["schedule.deadlines.read"];
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(dependencies.Settings);
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private static object Body(string name = "Aniversário da cidade", string scope = "municipality", string? state = "SP", string? municipality = "São Paulo",
        Guid? courtId = null, string startsOn = "2027-01-25", string endsOn = "2027-01-25", bool recurs = true) => new
        {
            name,
            kind = "holiday",
            scope,
            stateCode = state,
            municipality,
            courtId,
            startsOn,
            endsOn,
            recursYearly = recurs,
            note = (string?)null,
        };

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    [Fact]
    public async Task Controller_creates_lists_edits_and_deletes_entries()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);

        var (created, entry) = await SendAsync(office, HttpMethod.Post, Path, Body());
        created.ShouldBe(HttpStatusCode.Created, entry.ToString());
        var id = entry.GetProperty("id").GetGuid();
        entry.GetProperty("municipality").GetString().ShouldBe("São Paulo");
        entry.GetProperty("recursYearly").GetBoolean().ShouldBeTrue();

        await SendAsync(office, HttpMethod.Post, Path, Body("Feriado de janeiro", "office", null, null, startsOn: "2027-01-05", endsOn: "2027-01-05", recurs: false));
        var (listed, list) = await SendAsync(office, HttpMethod.Get, Path);
        listed.ShouldBe(HttpStatusCode.OK);
        list.EnumerateArray().Select(e => e.GetProperty("startsOn").GetString()).ToList().ShouldBe(["2027-01-05", "2027-01-25"]);

        var (updated, edited) = await SendAsync(office, HttpMethod.Put, $"{Path}/{id}", Body("Aniversário de Santos", state: "SP", municipality: "Santos"));
        updated.ShouldBe(HttpStatusCode.OK, edited.ToString());
        edited.GetProperty("name").GetString().ShouldBe("Aniversário de Santos");
        edited.GetProperty("municipality").GetString().ShouldBe("Santos");

        (await SendAsync(office, HttpMethod.Delete, $"{Path}/{id}")).Status.ShouldBe(HttpStatusCode.NoContent);
        (await SendAsync(office, HttpMethod.Delete, $"{Path}/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        var (_, remaining) = await SendAsync(office, HttpMethod.Get, Path);
        remaining.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Court_entry_is_created_for_an_existing_court()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);
        await using var scope = _factory.Services.CreateAsyncScope();
        var court = await scope.ServiceProvider.GetRequiredService<ICourtDirectory>().FindAsync(8, "26", Ct);
        court.ShouldNotBeNull();

        var (status, body) = await SendAsync(office, HttpMethod.Post, Path, Body("PJe instável", "court", null, null, court.Id, "2027-02-10", "2027-02-10", false));

        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        body.GetProperty("courtId").GetGuid().ShouldBe(court.Id);
    }

    [Fact]
    public async Task Lawyer_lists_but_cannot_change_entries()
    {
        var controller = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);
        var id = (await SendAsync(controller, HttpMethod.Post, Path, Body())).Body.GetProperty("id").GetGuid();
        var lawyer = await TestOrganizations.AddMemberAsync(dependencies, controller.OrganizationId, Guid.CreateVersion7(), LawyerPermissions);

        (await SendAsync(lawyer, HttpMethod.Get, Path)).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(lawyer, HttpMethod.Post, Path, Body())).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(lawyer, HttpMethod.Put, $"{Path}/{id}", Body())).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(lawyer, HttpMethod.Delete, $"{Path}/{id}")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invalid_entries_are_refused_on_create_and_update()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);
        var id = (await SendAsync(office, HttpMethod.Post, Path, Body())).Body.GetProperty("id").GetGuid();

        var (inverted, problem) = await SendAsync(office, HttpMethod.Post, Path, Body(startsOn: "2027-01-25", endsOn: "2027-01-20"));
        inverted.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(problem).ShouldBe("scheduling.calendar_entry_invalid");
        (await SendAsync(office, HttpMethod.Post, Path, Body("Estado sem UF", "state", null, null))).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, $"{Path}/{id}", Body(name: " "))).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_court_is_refused()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);
        var court = Body("Tribunal", "court", null, null, Guid.CreateVersion7(), "2027-02-10", "2027-02-10", false);

        var (status, problem) = await SendAsync(office, HttpMethod.Post, Path, court);

        status.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(problem).ShouldBe("scheduling.court_not_found");
        var id = (await SendAsync(office, HttpMethod.Post, Path, Body())).Body.GetProperty("id").GetGuid();
        OrganizationApi.Code((await SendAsync(office, HttpMethod.Put, $"{Path}/{id}", court)).Body).ShouldBe("scheduling.court_not_found");
    }

    [Fact]
    public async Task Another_office_neither_sees_nor_changes_the_entry()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);
        var other = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);
        var id = (await SendAsync(owner, HttpMethod.Post, Path, Body())).Body.GetProperty("id").GetGuid();

        (await SendAsync(other, HttpMethod.Get, Path)).Body.GetArrayLength().ShouldBe(0);
        (await SendAsync(other, HttpMethod.Put, $"{Path}/{id}", Body("Tomado"))).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(other, HttpMethod.Delete, $"{Path}/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(owner, HttpMethod.Get, Path)).Body.GetArrayLength().ShouldBe(1);
    }
}

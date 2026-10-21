using System.Globalization;
using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class ControlReportTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string[] ControllerPermissions = ["cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create", "schedule.control.read"];
    private static readonly string[] ClientRole = ["client"];
    private readonly ReportClock _clock = new();
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(dependencies.Settings, replaceServices: services => services.AddSingleton<IClock>(_clock));
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    private async Task<Guid> CreateCaseAsync(SeededMember member, Guid? assignee = null)
    {
        var person = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId,
            new { kind = "company", name = $"Cliente {Guid.CreateVersion7():N}", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", member.UserId, member.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = person, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = assignee ?? member.UserId } },
        });
    }

    [Fact]
    public async Task Stale_cases_use_creation_or_movement_occurrence_date_and_count_before_limit()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);
        var oldWithoutMovements = await CreateCaseAsync(office);
        var oldMovement = await CreateCaseAsync(office);
        var recentlyMoved = await CreateCaseAsync(office);
        _clock.UtcNow = _clock.UtcNow.AddDays(31);

        (await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{oldMovement}/movements", new
        {
            occurredAt = _clock.UtcNow.AddDays(-31),
            description = "Andamento antigo coletado hoje",
        })).Status.ShouldBe(HttpStatusCode.Created);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{recentlyMoved}/movements", new
        {
            occurredAt = _clock.UtcNow,
            description = "Andamento recente",
        })).Status.ShouldBe(HttpStatusCode.Created);

        var (status, report) = await SendAsync(office, HttpMethod.Get,
            "/api/v1/schedule/control?from=2026-09-01&until=2026-11-30&idleDays=30");

        status.ShouldBe(HttpStatusCode.OK, report.ToString());
        report.GetProperty("asOf").GetDateTimeOffset().ShouldBe(_clock.UtcNow);
        report.GetProperty("idleDays").GetInt32().ShouldBe(30);
        var stale = report.GetProperty("sections").EnumerateArray().Single(section => section.GetProperty("kind").GetString() == "staleCases");
        stale.GetProperty("count").GetInt32().ShouldBe(2);
        var items = stale.GetProperty("items").EnumerateArray().ToArray();
        items.Length.ShouldBe(2);
        items.Select(item => item.GetProperty("caseId").GetGuid()).ShouldContain(oldWithoutMovements);
        items.Select(item => item.GetProperty("caseId").GetGuid()).ShouldContain(oldMovement);
        items.Select(item => item.GetProperty("caseId").GetGuid()).ShouldNotContain(recentlyMoved);
        stale.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("caseId").GetGuid() == oldWithoutMovements)
            .GetProperty("at").GetDateTimeOffset().ShouldBe(_clock.UtcNow.AddDays(-31));
        stale.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("caseId").GetGuid() == oldMovement)
            .GetProperty("at").GetDateTimeOffset().ShouldBe(_clock.UtcNow.AddDays(-31));
        var (_, limitedReport) = await SendAsync(office, HttpMethod.Get,
            "/api/v1/schedule/control?from=2026-09-01&until=2026-11-30&idleDays=30&limit=1");
        var limitedSection = limitedReport.GetProperty("sections").EnumerateArray().Single(section => section.GetProperty("kind").GetString() == "staleCases");
        limitedSection.GetProperty("count").GetInt32().ShouldBe(2);
        limitedSection.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Stale_cases_respect_permission_scope_status_tenant_and_restrictions()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, ControllerPermissions);
        var ownReader = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            DataScope.Own, null, "cases.read", "schedule.control.read");
        var officeReader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            "cases.read", "schedule.control.read");
        var ownCase = await CreateCaseAsync(office, ownReader.UserId);
        var outsideScopeCase = await CreateCaseAsync(office);
        var restrictedCase = await CreateCaseAsync(office);
        var closedCase = await CreateCaseAsync(office);
        var otherOffice = await TestOrganizations.CreateWithMemberAsync(dependencies, "cases.create", "cases.read", "cases.edit", "crm.people.create", "schedule.control.read");
        var foreignCase = await CreateCaseAsync(otherOffice);

        using (AmbientContext.Begin(office.OrganizationId, "control-report-tests"))
        await using (var provider = dependencies.PlatformServices())
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LookupTableSeeder>().SeedAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().SaveChangesAsync(Ct);
        }
        var (_, options) = await SendAsync(office, HttpMethod.Get, "/api/v1/cases/options");
        var closingReason = options.GetProperty("closingReasons")[0].GetProperty("id").GetGuid();
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{outsideScopeCase}/status", new { status = "suspended" })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{restrictedCase}/restrictions", new
        {
            allowed = Array.Empty<object>(),
            blocked = new[] { new { userId = ownReader.UserId }, new { userId = officeReader.UserId } },
        })).Status
            .ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{closedCase}/close", new
        {
            reasonId = closingReason,
            outcome = "settlement",
            closedOn = _clock.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        })).Status.ShouldBe(HttpStatusCode.OK);

        _clock.UtcNow = _clock.UtcNow.AddDays(31);
        var (reportStatus, report) = await SendAsync(ownReader, HttpMethod.Get,
            "/api/v1/schedule/control?from=2026-09-01&until=2026-11-30&idleDays=30");
        reportStatus.ShouldBe(HttpStatusCode.OK, report.ToString());
        var section = report.GetProperty("sections").EnumerateArray().Single(item => item.GetProperty("kind").GetString() == "staleCases");
        section.GetProperty("count").GetInt32().ShouldBe(1);
        section.GetProperty("items")[0].GetProperty("caseId").GetGuid().ShouldBe(ownCase);
        section.ToString().ShouldNotContain(outsideScopeCase.ToString());
        section.ToString().ShouldNotContain(restrictedCase.ToString());
        section.ToString().ShouldNotContain(closedCase.ToString());
        section.ToString().ShouldNotContain(foreignCase.ToString());

        var (_, officeReport) = await SendAsync(officeReader, HttpMethod.Get,
            "/api/v1/schedule/control?from=2026-09-01&until=2026-11-30&idleDays=30");
        var officeSection = officeReport.GetProperty("sections").EnumerateArray().Single(item => item.GetProperty("kind").GetString() == "staleCases");
        officeSection.GetProperty("count").GetInt32().ShouldBe(1);
        officeSection.GetProperty("items")[0].GetProperty("caseId").GetGuid().ShouldBe(ownCase);
        officeSection.ToString().ShouldNotContain(restrictedCase.ToString());

        var noControlPermission = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "cases.read");
        (await SendAsync(noControlPermission, HttpMethod.Get, "/api/v1/schedule/control")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/schedule/control?idleDays=0")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/schedule/control?from=2026-11-30&until=2026-09-01")).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cases_read_and_control_read_are_sufficient_without_publications_read()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, "cases.create", "cases.read", "cases.edit", "crm.people.create", "schedule.control.read");
        var caseId = await CreateCaseAsync(office);
        _clock.UtcNow = _clock.UtcNow.AddDays(31);

        var (status, report) = await SendAsync(office, HttpMethod.Get, "/api/v1/schedule/control?from=2026-09-01&until=2026-11-30");

        status.ShouldBe(HttpStatusCode.OK, report.ToString());
        report.GetProperty("sections").EnumerateArray().Single(section => section.GetProperty("kind").GetString() == "staleCases")
            .GetProperty("items")[0].GetProperty("caseId").GetGuid().ShouldBe(caseId);
    }

    private sealed class ReportClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 21, 15, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(UtcNow.DateTime);
    }
}

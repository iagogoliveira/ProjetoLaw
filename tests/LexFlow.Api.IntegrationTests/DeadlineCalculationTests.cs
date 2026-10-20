using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Calculadora de prazos ponta a ponta: seed da plataforma, local do processo, calendário do escritório e acesso.</summary>
public sealed class DeadlineCalculationTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private const string Path = "/api/v1/deadlines/calculate";
    private const string TjspCnj = "2238058-98.2025.8.26.8434";
    private const string OtherTjspCnj = "1234567-76.2025.8.26.0100";
    private const string Trf1Cnj = "6616920-53.2022.4.01.1974";
    private static readonly string[] Permissions = ["cases.create", "cases.read", "crm.people.create", "schedule.deadlines.read", "schedule.deadlines.manage"];
    private static readonly string[] ReadOnly = ["cases.read", "schedule.deadlines.read"];
    private static readonly string[] ClientRole = ["client"];
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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

    [Fact]
    public async Task State_court_case_uses_the_state_holidays_and_shows_them_in_the_memory()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
        var caseId = await CaseAsync(office, TjspCnj, "São Paulo");

        var (status, body) = await CalculateAsync(office, new { caseId, startKind = "publication", triggerDate = "2026-07-06", days = 5, mode = "businessDays" });

        status.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("fatalDueOn").GetString().ShouldBe("2026-07-14");
        body.GetProperty("stateCode").GetString().ShouldBe("SP");
        body.GetProperty("municipality").GetString().ShouldBe("São Paulo");
        body.GetProperty("federalJustice").GetBoolean().ShouldBeFalse();
        Reasons(body).ShouldContain("Revolução Constitucionalista de 1932 (Lei estadual 9.497/1997)");
    }

    [Fact]
    public async Task Office_municipal_holiday_applies_only_to_cases_of_that_municipality()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
        var inCity = await CaseAsync(office, TjspCnj, "Sao  paulo");
        var elsewhere = await CaseAsync(office, OtherTjspCnj, "Campinas");
        await AddEntryAsync(office, new CalendarEntryInput("Feriado municipal de teste", CalendarEntryKind.Holiday, CalendarEntryScope.Municipality,
            "SP", "São Paulo", null, new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 8), RecursYearly: false, Note: null));

        var city = await CalculateAsync(office, new { caseId = inCity, startKind = "publication", triggerDate = "2026-07-06", days = 5, mode = "businessDays" });
        var other = await CalculateAsync(office, new { caseId = elsewhere, startKind = "publication", triggerDate = "2026-07-06", days = 5, mode = "businessDays" });

        city.Body.GetProperty("fatalDueOn").GetString().ShouldBe("2026-07-15");
        Reasons(city.Body).ShouldContain("Feriado municipal de teste");
        other.Body.GetProperty("fatalDueOn").GetString().ShouldBe("2026-07-14");
    }

    [Fact]
    public async Task Multi_state_federal_court_applies_federal_justice_days_without_presuming_a_state()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
        var caseId = await CaseAsync(office, Trf1Cnj, null);

        var (status, body) = await CalculateAsync(office, new { caseId, startKind = "publication", triggerDate = "2026-03-30", days = 5, mode = "businessDays" });

        status.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("federalJustice").GetBoolean().ShouldBeTrue();
        body.GetProperty("stateCode").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("fatalDueOn").GetString().ShouldBe("2026-04-09");
    }

    [Fact]
    public async Task Location_can_be_informed_without_a_case_and_the_recess_suspends_the_count()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);

        var rio = await CalculateAsync(office, new { stateCode = "rj", startKind = "publication", triggerDate = "2026-04-17", days = 5, mode = "businessDays" });
        var recess = await CalculateAsync(office, new { startKind = "djenAvailability", triggerDate = "2026-12-17", days = 15, mode = "businessDays", internalLeadDays = 2 });

        rio.Body.GetProperty("fatalDueOn").GetString().ShouldBe("2026-04-28");
        rio.Body.GetProperty("stateCode").GetString().ShouldBe("RJ");
        recess.Body.GetProperty("startsOn").GetString().ShouldBe("2027-01-21");
        recess.Body.GetProperty("fatalDueOn").GetString().ShouldBe("2027-02-10");
        recess.Body.GetProperty("internalDueOn").GetString().ShouldBe("2027-02-08");
        Reasons(recess.Body).ShouldContain("Suspensão dos prazos (recesso forense) (CPC, art. 220)");
    }

    [Fact]
    public async Task Invalid_requests_are_rejected_with_stable_codes()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);

        var noDays = await CalculateAsync(office, new { startKind = "publication", triggerDate = "2026-03-02", days = 0, mode = "businessDays" });
        var court = await CalculateAsync(office, new { courtId = Guid.CreateVersion7(), startKind = "publication", triggerDate = "2026-03-02", days = 5, mode = "businessDays" });
        var consulted = await CalculateAsync(office, new { startKind = "publication", triggerDate = "2026-03-02", consultedOn = "2026-03-03", days = 5, mode = "businessDays" });

        noDays.Status.ShouldBe(HttpStatusCode.BadRequest);
        noDays.Body.GetProperty("code").GetString().ShouldBe("validation.failed");
        court.Status.ShouldBe(HttpStatusCode.BadRequest);
        court.Body.GetProperty("code").GetString().ShouldBe("scheduling.court_not_found");
        consulted.Status.ShouldBe(HttpStatusCode.BadRequest);
        consulted.Body.GetProperty("code").GetString().ShouldBe("scheduling.calculation_invalid");
    }

    [Fact]
    public async Task Calculation_requires_the_manage_permission_and_access_to_the_case()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
        var reader = await TestOrganizations.CreateWithMemberAsync(dependencies, ReadOnly);
        var caseId = await CaseAsync(office, TjspCnj, null);

        var denied = await CalculateAsync(reader, new { startKind = "publication", triggerDate = "2026-03-02", days = 5, mode = "businessDays" });
        var otherOffice = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
        var crossTenant = await CalculateAsync(otherOffice, new { caseId, startKind = "publication", triggerDate = "2026-03-02", days = 5, mode = "businessDays" });

        denied.Status.ShouldBe(HttpStatusCode.Forbidden);
        crossTenant.Status.ShouldBe(HttpStatusCode.NotFound);
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> CalculateAsync(SeededMember member, object body) =>
        _api.SendAsync(HttpMethod.Post, Path, member.UserId, member.OrganizationId, body);

    private async Task<Guid> CaseAsync(SeededMember member, string cnj, string? district)
    {
        var client = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId, new { kind = "company", name = "Cliente", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", member.UserId, member.OrganizationId, new
        {
            kind = "judicial",
            cnjNumber = cnj,
            instance = "first",
            district,
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = member.UserId } },
        });
    }

    private async Task AddEntryAsync(SeededMember office, CalendarEntryInput input)
    {
        using var ambient = AmbientContext.Begin(office.OrganizationId, "calculation-test");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(office.OrganizationId);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        db.CalendarEntries.Add(CalendarEntry.Create(input).Value);
        await db.SaveChangesAsync(Ct);
    }

    private static List<string?> Reasons(JsonElement body) =>
        [.. body.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("reason").GetString())];
}

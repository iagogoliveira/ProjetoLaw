using System.Net;
using System.Text.Json;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Linha do tempo do processo e operações em lote (Etapa 5, PR 5D) pela API real.</summary>
public sealed class CaseTimelineAndBatchTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static readonly string[] Everything =
    [
        "cases.read", "cases.create", "cases.edit", "cases.risk.assess", "cases.settlements.approve", "crm.people.create", "settings.custom_fields.read",
    ];

    private static readonly string[] ClientRole = ["client"];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record Office(Guid Organization, Guid User, Guid Client, Guid Phase, Guid ClosingReason, Guid ClaimType);

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(Office office, HttpMethod method, string path, object? body = null, Guid? user = null) =>
        _api.SendAsync(method, path, user ?? office.User, office.Organization, body);

    private async Task<Office> OfficeAsync()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies, Everything);
        using (AmbientContext.Begin(member.OrganizationId, "timeline-tests"))
        await using (var provider = dependencies.PlatformServices())
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LookupTableSeeder>().SeedAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().SaveChangesAsync(Ct);
        }

        var client = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId, new { kind = "company", name = "Cliente Gama Ltda.", roles = ClientRole });
        var (_, tables) = await _api.SendAsync(HttpMethod.Get, "/api/v1/lookup-tables", member.UserId, member.OrganizationId);
        Guid First(string key) => tables.EnumerateArray()
            .Single(t => t.GetProperty("systemKey").ValueKind == JsonValueKind.String && t.GetProperty("systemKey").GetString() == key)
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        return new Office(member.OrganizationId, member.UserId, client, First(SystemLookupTables.CasePhases), First(SystemLookupTables.ClosingReasons), First(SystemLookupTables.Claims));
    }

    private Task<Guid> CaseAsync(Office office, Guid? lawyer = null) =>
        _api.CreateAsync("/api/v1/cases", office.User, office.Organization, new
        {
            kind = "extrajudicial",
            parties = new object[] { new { personId = office.Client, pole = "active", isClient = true } },
            assignments = new object[] { new { role = "lawyer", userId = lawyer ?? office.User } },
        });

    [Fact]
    public async Task The_timeline_gathers_what_happened_newest_first_with_pages_and_filters()
    {
        var office = await OfficeAsync();
        var id = await CaseAsync(office);
        var basePath = $"/api/v1/cases/{id}";
        await SendAsync(office, HttpMethod.Post, $"{basePath}/status", new { status = "suspended" });
        await SendAsync(office, HttpMethod.Post, $"{basePath}/status", new { status = "active" });
        var claim = await _api.CreateAsync($"{basePath}/claims", office.User, office.Organization,
            new { claimTypeId = office.ClaimType, valuations = new object[] { new { instance = "first", requested = 1_000m } } });
        await SendAsync(office, HttpMethod.Put, $"{basePath}/claims/{claim}",
            new { claimTypeId = office.ClaimType, valuations = new object[] { new { instance = "first", requested = 2_000m } } });
        await SendAsync(office, HttpMethod.Post, $"{basePath}/risk-assessments", new { probability = "possible", estimatedLoss = 500m });
        var hearing = (await SendAsync(office, HttpMethod.Post, $"{basePath}/hearings",
            new { type = "conciliation", format = "virtual", scheduledAt = "2026-11-10T14:00:00Z", meetingLink = "https://meet.example.com/x" })).Body.GetProperty("id").GetGuid();
        await SendAsync(office, HttpMethod.Post, $"{basePath}/hearings/{hearing}/reschedule", new { newDate = "2026-11-20T14:00:00Z", reason = "Pauta cheia" });
        await SendAsync(office, HttpMethod.Post, $"{basePath}/close", new { reasonId = office.ClosingReason, outcome = "settlement", closedOn = "2026-10-18", note = "Acordo" });

        var (firstStatus, first) = await SendAsync(office, HttpMethod.Get, $"{basePath}/timeline?limit=4");
        var cursor = first.GetProperty("nextCursor").GetString();
        var (_, second) = await SendAsync(office, HttpMethod.Get, $"{basePath}/timeline?limit=50&cursor={cursor}");
        var (_, hearings) = await SendAsync(office, HttpMethod.Get, $"{basePath}/timeline?kind=litigation.hearing");
        var (badStatus, _) = await SendAsync(office, HttpMethod.Get, $"{basePath}/timeline?cursor=nao-e-cursor");

        firstStatus.ShouldBe(HttpStatusCode.OK, first.ToString());
        var kinds = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray())
            .Select(e => e.GetProperty("kind").GetString()).ToList();
        // Cadastro, 3 mudanças de situação (suspensão, reativação, encerramento), pedido, alteração de valor, risco,
        // audiência agendada e redesignada.
        kinds.Count.ShouldBe(9);
        kinds[0].ShouldBe("litigation.status_changed");
        kinds[^1].ShouldBe("litigation.case_created");
        kinds.Count(k => k == "litigation.status_changed").ShouldBe(3);
        kinds.ShouldContain("litigation.claim_value_changed");
        kinds.ShouldContain("litigation.risk_assessed");
        second.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        first.GetProperty("items")[0].GetProperty("data").GetProperty("to").GetString().ShouldBe("Closed");
        hearings.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("kind").GetString())
            .ShouldBe(["litigation.hearing_rescheduled", "litigation.hearing_scheduled"]);
        badStatus.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_batch_updates_the_reachable_open_cases_and_reports_the_rest()
    {
        var office = await OfficeAsync();
        var partner = await TestOrganizations.AddScopedMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), DataScope.Organization, null, Everything);
        var lawyer = await TestOrganizations.AddScopedMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), DataScope.Own, null, ["cases.read", "cases.edit"]);
        var mine = await CaseAsync(office, lawyer.UserId);
        var other = await CaseAsync(office);
        var closed = await CaseAsync(office, lawyer.UserId);
        await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{closed}/close", new { reasonId = office.ClosingReason, outcome = "denied", closedOn = "2026-10-01" });

        var (redistributeStatus, redistributed) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases/batch",
            new { caseIds = new[] { mine, other, closed, Guid.CreateVersion7() }, action = "redistribute", fromUserId = lawyer.UserId, toUserId = partner.UserId, role = "partner" },
            lawyer.UserId);
        var (phaseStatus, phase) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases/batch", new { caseIds = new[] { mine, other }, action = "changePhase", phaseId = office.Phase });
        var (suspendStatus, suspended) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases/batch", new { caseIds = new[] { mine, other }, action = "suspend", note = "Aguardando perícia" });
        var (_, mineAfter) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{mine}");
        var (_, timeline) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{other}/timeline?kind=litigation.status_changed");
        var (outsiderStatus, outsider) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases/batch",
            new { caseIds = new[] { mine }, action = "redistribute", toUserId = Guid.CreateVersion7() });
        var (tooManyStatus, _) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases/batch",
            new { caseIds = Enumerable.Range(0, 1001).Select(_ => Guid.CreateVersion7()).ToArray(), action = "suspend" });

        redistributeStatus.ShouldBe(HttpStatusCode.OK, redistributed.ToString());
        redistributed.GetProperty("updated").GetInt32().ShouldBe(1);
        redistributed.GetProperty("skipped").EnumerateArray().Select(s => s.GetProperty("code").GetString()).OrderBy(c => c, StringComparer.Ordinal)
            .ShouldBe(["litigation.case_closed", "litigation.case_not_found", "litigation.case_not_found"]);
        phaseStatus.ShouldBe(HttpStatusCode.OK);
        phase.GetProperty("updated").GetInt32().ShouldBe(2);
        suspendStatus.ShouldBe(HttpStatusCode.OK);
        suspended.GetProperty("updated").GetInt32().ShouldBe(2);
        mineAfter.GetProperty("status").GetString().ShouldBe("suspended");
        mineAfter.GetProperty("phaseId").GetGuid().ShouldBe(office.Phase);
        mineAfter.GetProperty("assignments").EnumerateArray().Select(a => (a.GetProperty("role").GetString(), a.GetProperty("userId").GetGuid()))
            .ShouldBe([("partner", partner.UserId)]);
        timeline.GetProperty("items")[0].GetProperty("data").GetProperty("note").GetString().ShouldBe("Aguardando perícia");
        outsiderStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(outsider).ShouldBe("litigation.responsible_invalid");
        tooManyStatus.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Whoever_reads_cases_gets_the_form_options_without_settings_permissions()
    {
        var office = await OfficeAsync();
        var reader = await TestOrganizations.AddScopedMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), DataScope.Own, null, ["cases.read"]);
        var admin = await TestOrganizations.AddMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), "settings.structure.manage");
        await _api.CreateAsync("/api/v1/organization/practice-areas", admin.UserId, office.Organization, new { name = "Trabalhista" });

        var (status, options) = await SendAsync(office, HttpMethod.Get, "/api/v1/cases/options", user: reader.UserId);
        var (structureStatus, _) = await SendAsync(office, HttpMethod.Get, "/api/v1/organization/practice-areas", user: reader.UserId);

        status.ShouldBe(HttpStatusCode.OK, options.ToString());
        options.GetProperty("practiceAreas").EnumerateArray().Select(a => a.GetProperty("name").GetString()).ShouldBe(["Trabalhista"]);
        options.GetProperty("phases")[0].GetProperty("id").GetGuid().ShouldBe(office.Phase);
        options.GetProperty("closingReasons").GetArrayLength().ShouldBeGreaterThan(0);
        options.GetProperty("claimTypes").GetArrayLength().ShouldBeGreaterThan(0);
        structureStatus.ShouldBe(HttpStatusCode.Forbidden);
    }
}

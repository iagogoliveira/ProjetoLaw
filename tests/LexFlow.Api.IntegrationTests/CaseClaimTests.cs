using System.Net;
using System.Text.Json;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Pedidos do processo (Etapa 5, PR 5C1) pela API real: valores por instância, histórico das alterações, atualização
/// monetária com a tabela da plataforma (seed real), totais e o alcance do processo. Também a calculadora e a lista de
/// índices.
/// </summary>
public sealed class CaseClaimTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static readonly string[] Everything = ["cases.read", "cases.create", "cases.edit", "crm.people.create", "settings.custom_fields.read"];
    private static readonly string[] Reader = ["cases.read", "cases.edit"];
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

    private sealed record Office(Guid Organization, Guid User, Guid Case, Guid ClaimType);

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(Office office, HttpMethod method, string path, object? body = null, Guid? user = null) =>
        _api.SendAsync(method, path, user ?? office.User, office.Organization, body);

    private async Task<Office> OfficeAsync()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies, Everything);
        using (AmbientContext.Begin(member.OrganizationId, "claim-tests"))
        await using (var provider = dependencies.PlatformServices())
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LookupTableSeeder>().SeedAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().SaveChangesAsync(Ct);
        }

        var client = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId, new { kind = "company", name = "Banco Alfa S.A.", roles = ClientRole });
        var caseId = await _api.CreateAsync("/api/v1/cases", member.UserId, member.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new object[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new object[] { new { role = "lawyer", userId = member.UserId } },
        });
        var (_, tables) = await _api.SendAsync(HttpMethod.Get, "/api/v1/lookup-tables", member.UserId, member.OrganizationId);
        var claimType = tables.EnumerateArray()
            .Single(t => t.GetProperty("systemKey").ValueKind == JsonValueKind.String && t.GetProperty("systemKey").GetString() == SystemLookupTables.Claims)
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        return new Office(member.OrganizationId, member.UserId, caseId, claimType);
    }

    private static object Claim(Office office, object[] valuations, object? correction = null) => new
    {
        claimTypeId = office.ClaimType,
        description = "Horas extras",
        correction,
        valuations,
    };

    private static readonly object Ipca = new { index = "ipcaE", correctFrom = "2024-01-10", interestFrom = "2024-02-05", monthlyInterestPercent = 1m };

    [Fact]
    public async Task Claims_are_updated_with_the_platform_indexes_and_totaled_per_instance()
    {
        var office = await OfficeAsync();
        var (createdStatus, created) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/claims",
            Claim(office, [new { instance = "first", requested = 20_000m, estimated = 10_000m }], Ipca));
        await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/claims", Claim(office, [new { instance = "first", requested = 5_000m }]));

        var (listStatus, list) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/claims?updateTo=2025-03-20");
        var (plainStatus, plain) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/claims");

        createdStatus.ShouldBe(HttpStatusCode.Created, created.ToString());
        created.GetProperty("correction").GetProperty("index").GetString().ShouldBe("ipcaE");
        listStatus.ShouldBe(HttpStatusCode.OK, list.ToString());
        var first = list.GetProperty("items")[0].GetProperty("valuations")[0];
        first.GetProperty("updatedRequested").GetDecimal().ShouldBe(24_087.23m); // caso C5 da ADR 0027
        first.GetProperty("updatedEstimated").GetDecimal().ShouldBe(12_043.61m);
        first.GetProperty("updatedAwarded").ValueKind.ShouldBe(JsonValueKind.Null);
        list.GetProperty("items")[1].GetProperty("valuations")[0].GetProperty("updatedRequested").ValueKind.ShouldBe(JsonValueKind.Null);
        var totals = list.GetProperty("totals")[0];
        totals.GetProperty("requested").GetDecimal().ShouldBe(25_000m);
        totals.GetProperty("updatedRequested").GetDecimal().ShouldBe(29_087.23m);
        totals.GetProperty("updatedEstimated").GetDecimal().ShouldBe(12_043.61m);
        plainStatus.ShouldBe(HttpStatusCode.OK);
        plain.GetProperty("totals")[0].GetProperty("updatedRequested").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_month_not_yet_published_is_reported_per_claim()
    {
        var office = await OfficeAsync();
        await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/claims", Claim(office, [new { instance = "first", requested = 1_000m }], Ipca));

        var (status, list) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/claims?updateTo=2027-01-15");

        status.ShouldBe(HttpStatusCode.OK);
        var claim = list.GetProperty("items")[0];
        claim.GetProperty("correctionError").GetString().ShouldBe("platform.monetary_index_missing");
        claim.GetProperty("valuations")[0].GetProperty("updatedRequested").ValueKind.ShouldBe(JsonValueKind.Null);
        list.GetProperty("totals")[0].GetProperty("updatedRequested").GetDecimal().ShouldBe(1_000m);
    }

    [Fact]
    public async Task Every_value_change_goes_to_the_history_and_deleting_hides_the_claim()
    {
        var office = await OfficeAsync();
        var id = await _api.CreateAsync($"/api/v1/cases/{office.Case}/claims", office.User, office.Organization,
            Claim(office, [new { instance = "first", requested = 50_000m }]));

        var (editStatus, edited) = await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{office.Case}/claims/{id}",
            Claim(office, [new { instance = "first", requested = 50_000m, awarded = 30_000m }, new { instance = "second", awarded = 35_000m }]));
        var (historyStatus, history) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/claims/{id}/history");
        var (deleteStatus, _) = await SendAsync(office, HttpMethod.Delete, $"/api/v1/cases/{office.Case}/claims/{id}");
        var (_, after) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/claims");
        var (goneStatus, gone) = await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{office.Case}/claims/{id}", Claim(office, []));

        editStatus.ShouldBe(HttpStatusCode.OK, edited.ToString());
        edited.GetProperty("valuations").GetArrayLength().ShouldBe(2);
        historyStatus.ShouldBe(HttpStatusCode.OK);
        history.EnumerateArray().Select(h => (h.GetProperty("instance").GetString(), h.GetProperty("kind").GetString(), h.GetProperty("newAmount").GetDecimal()))
            .ShouldBe([("first", "requested", 50_000m), ("first", "awarded", 30_000m), ("second", "awarded", 35_000m)]);
        history.EnumerateArray().ShouldAllBe(h => h.GetProperty("changedBy").GetGuid() == office.User);
        deleteStatus.ShouldBe(HttpStatusCode.NoContent);
        after.GetProperty("items").GetArrayLength().ShouldBe(0);
        goneStatus.ShouldBe(HttpStatusCode.NotFound);
        OrganizationApi.Code(gone).ShouldBe("litigation.claim_not_found");
    }

    [Fact]
    public async Task Invalid_claims_are_refused_and_a_closed_case_takes_no_changes()
    {
        var office = await OfficeAsync();
        var (typeStatus, type) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/claims",
            new { claimTypeId = Guid.CreateVersion7(), valuations = Array.Empty<object>() });
        var (selicStatus, selic) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/claims",
            Claim(office, [], new { index = "selic", correctFrom = "2024-01-01", interestFrom = "2024-01-01", monthlyInterestPercent = 1m }));
        var (twiceStatus, twice) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/claims",
            Claim(office, [new { instance = "first", requested = 1m }, new { instance = "first", requested = 2m }]));
        var (noCaseStatus, _) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{Guid.CreateVersion7()}/claims");

        var (_, tables) = await SendAsync(office, HttpMethod.Get, "/api/v1/lookup-tables");
        var reason = tables.EnumerateArray()
            .Single(t => t.GetProperty("systemKey").ValueKind == JsonValueKind.String && t.GetProperty("systemKey").GetString() == SystemLookupTables.ClosingReasons)
            .GetProperty("items")[0].GetProperty("id").GetGuid();
        var claim = await _api.CreateAsync($"/api/v1/cases/{office.Case}/claims", office.User, office.Organization, Claim(office, []));
        await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/close", new { reasonId = reason, outcome = "settlement", closedOn = "2026-09-01" });
        var (closedStatus, closed) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{office.Case}/claims", Claim(office, []));
        var (closedDeleteStatus, _) = await SendAsync(office, HttpMethod.Delete, $"/api/v1/cases/{office.Case}/claims/{claim}");

        typeStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(type).ShouldBe("litigation.claim_type_invalid");
        selicStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(selic).ShouldBe("litigation.claim_correction_invalid");
        twiceStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(twice).ShouldBe("litigation.valuation_instance_duplicated");
        noCaseStatus.ShouldBe(HttpStatusCode.NotFound);
        closedStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(closed).ShouldBe("litigation.case_closed");
        closedDeleteStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Claims_follow_the_reach_of_the_case()
    {
        var office = await OfficeAsync();
        var claim = await _api.CreateAsync($"/api/v1/cases/{office.Case}/claims", office.User, office.Organization, Claim(office, []));
        var outsider = await TestOrganizations.AddScopedMemberAsync(dependencies, office.Organization, Guid.CreateVersion7(), DataScope.Own, null, Reader);

        var (listStatus, _) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/claims", user: outsider.UserId);
        var (historyStatus, _) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{office.Case}/claims/{claim}/history", user: outsider.UserId);
        var (editStatus, _) = await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{office.Case}/claims/{claim}", Claim(office, []), outsider.UserId);

        listStatus.ShouldBe(HttpStatusCode.NotFound);
        historyStatus.ShouldBe(HttpStatusCode.NotFound);
        editStatus.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_calculator_and_the_index_list_use_the_platform_table()
    {
        var office = await OfficeAsync();

        var (calcStatus, calc) = await SendAsync(office, HttpMethod.Post, "/api/v1/monetary-corrections",
            new { principal = 10_000m, index = "inpc", correctFrom = "2020-01-15", until = "2021-01-15" });
        var (listStatus, list) = await SendAsync(office, HttpMethod.Get, "/api/v1/monetary-indexes?index=selic&from=2023-01-01&to=2023-03-01");
        var (badStatus, bad) = await SendAsync(office, HttpMethod.Get, "/api/v1/monetary-indexes?index=igpm");
        var (missingStatus, missing) = await SendAsync(office, HttpMethod.Get, "/api/v1/monetary-indexes");
        var (rangeStatus, range) = await SendAsync(office, HttpMethod.Get, "/api/v1/monetary-indexes?index=tr&from=2023-03-01&to=2023-01-01");
        var (selicStatus, selic) = await SendAsync(office, HttpMethod.Post, "/api/v1/monetary-corrections",
            new { principal = 1m, index = "selic", correctFrom = "2020-01-15", until = "2021-01-15", interestFrom = "2020-01-15", monthlyInterestPercent = 1m });

        calcStatus.ShouldBe(HttpStatusCode.OK, calc.ToString());
        calc.GetProperty("correctedAmount").GetDecimal().ShouldBe(10_544.73m); // caso C1
        calc.GetProperty("months").GetArrayLength().ShouldBe(12);
        listStatus.ShouldBe(HttpStatusCode.OK);
        list.EnumerateArray().Select(i => i.GetProperty("percent").GetDecimal()).ShouldBe([1.12m, 0.92m, 1.17m]);
        list[0].GetProperty("source").GetString().ShouldBe("seed");
        badStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(bad).ShouldBe("platform.monetary_index_invalid");
        missingStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(missing).ShouldBe("platform.monetary_index_invalid");
        rangeStatus.ShouldBe(HttpStatusCode.BadRequest);
        range.GetProperty("errors").EnumerateObject().Single().Value[0].GetProperty("code").GetString().ShouldBe("platform.correction_period_invalid");
        selicStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(selic).ShouldBe("platform.correction_selic_with_interest");
    }
}

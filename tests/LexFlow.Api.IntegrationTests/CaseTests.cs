using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Crm.Contracts;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Litigation.Infrastructure.People;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Processos e casos (Etapa 5, PR 5A) pela API real: cadastro com CNJ e tribunal pré-preenchido, numeração interna por
/// área e ano, partes do Crm, responsáveis, encerramento, hierarquia e as partes dos processos no conflito de
/// interesses (critério de aceite da Etapa 4, agora com processos reais).
/// </summary>
public sealed class CaseTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    /// <summary>CNJs válidos: TJSP (8.26) e TRF1 (4.01).</summary>
    private const string TjspCnj = "2238058-98.2025.8.26.8434";
    private const string Trf1Cnj = "6616920-53.2022.4.01.1974";

    private static readonly string[] Everything =
    [
        "cases.read", "cases.create", "cases.edit", "cases.delete", "crm.people.read", "crm.people.create", "crm.conflicts.check",
        "settings.structure.manage", "settings.organization.manage", "settings.custom_fields.read",
    ];

    private static readonly string[] ClientRole = ["client"];
    private static readonly string[] WitnessRole = ["witness"];

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

    private sealed record Office(Guid Organization, Guid User, Guid Client, Guid Opponent);

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(Office office, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, office.User, office.Organization, body);

    private async Task<Office> OfficeAsync(bool seedLookups = false)
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies, Everything);
        if (seedLookups)
        {
            using (AmbientContext.Begin(member.OrganizationId, "case-tests"))
            await using (var provider = dependencies.PlatformServices())
            await using (var scope = provider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<LookupTableSeeder>().SeedAsync(Ct);
                await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().SaveChangesAsync(Ct);
            }
        }

        var client = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId, new { kind = "company", name = "Banco Alfa S.A.", roles = ClientRole });
        var opponent = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId, new { kind = "company", name = "Construtora Rival Ltda.", document = "11444777000161", roles = WitnessRole });
        return new Office(member.OrganizationId, member.UserId, client, opponent);
    }

    private static object Case(Office office, string? cnj = TjspCnj, Guid? area = null, string kind = "judicial", object[]? assignments = null) => new
    {
        kind,
        cnjNumber = cnj,
        instance = "first",
        practiceAreaId = area,
        claimValue = 150_000m,
        parties = new object[]
        {
            new { personId = office.Client, pole = "active", isClient = true },
            new { personId = office.Opponent, pole = "passive", isClient = false, counselName = "Dra. Ana", counselOab = "SP 123456" },
        },
        assignments = assignments ?? [new { role = "lawyer", userId = office.User }],
    };

    private async Task<JsonElement> CreateCaseAsync(Office office, object body)
    {
        var (status, created) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases", body);
        status.ShouldBe(HttpStatusCode.Created, created.ToString());
        return created;
    }

    private static Guid IdOf(JsonElement element) => element.GetProperty("id").GetGuid();

    /// <summary>O ano do cadastro, pelo relógio da API (a numeração usa o ano de hoje no fuso legal).</summary>
    private int Year => _factory.Services.GetRequiredService<IClock>().Today.Year;

    [Fact]
    public async Task A_judicial_case_gets_the_court_from_the_cnj_and_a_folder_numbered_by_area_and_year()
    {
        var office = await OfficeAsync();
        var area = await _api.CreateAsync("/api/v1/organization/practice-areas", office.User, office.Organization, new { name = "Trabalhista" });

        var first = await CreateCaseAsync(office, Case(office, area: area));
        var second = await CreateCaseAsync(office, Case(office, cnj: Trf1Cnj, area: area));
        var administrative = await CreateCaseAsync(office, Case(office, cnj: null, kind: "administrative"));
        var (_, fetched) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{IdOf(first)}");

        var year = Year;
        first.GetProperty("internalFolder").GetString().ShouldBe($"TRAB-{year}-00001");
        second.GetProperty("internalFolder").GetString().ShouldBe($"TRAB-{year}-00002");
        administrative.GetProperty("internalFolder").GetString().ShouldBe($"GERAL-{year}-00001");
        fetched.GetProperty("cnjNumber").GetString().ShouldBe(TjspCnj);
        fetched.GetProperty("court").GetProperty("acronym").GetString().ShouldBe("TJSP");
        second.GetProperty("court").GetProperty("acronym").GetString().ShouldBe("TRF1");
        var parties = fetched.GetProperty("parties").EnumerateArray().ToList();
        parties.Select(p => p.GetProperty("name").GetString()).ShouldBe(["Banco Alfa S.A.", "Construtora Rival Ltda."], ignoreOrder: true);
        parties.Single(p => !p.GetProperty("isClient").GetBoolean()).GetProperty("counselOab").GetString().ShouldBe("SP 123456");
        fetched.GetProperty("assignments")[0].GetProperty("userId").GetGuid().ShouldBe(office.User);
    }

    /// <summary>Regressão (PR 5B): parte e responsável novos numa edição são inseridos (o id vem do domínio).</summary>
    [Fact]
    public async Task An_update_adds_parties_and_responsibles()
    {
        var office = await OfficeAsync();
        var created = await CreateCaseAsync(office, Case(office, assignments: []));
        var witness = await _api.CreateAsync("/api/v1/people", office.User, office.Organization, new { kind = "individual", name = "Testemunha", roles = WitnessRole });

        var (status, updated) = await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{IdOf(created)}", new
        {
            cnjNumber = TjspCnj,
            parties = new object[]
            {
                new { personId = office.Client, pole = "active", isClient = true },
                new { personId = office.Opponent, pole = "passive", isClient = false },
                new { personId = witness, pole = "third", isClient = false },
            },
            assignments = new[] { new { role = "partner", userId = office.User } },
        });

        status.ShouldBe(HttpStatusCode.OK, updated.ToString());
        updated.GetProperty("parties").GetArrayLength().ShouldBe(3);
        updated.GetProperty("assignments")[0].GetProperty("role").GetString().ShouldBe("partner");
    }

    [Fact]
    public async Task The_cnj_is_checked_unique_in_the_office_and_described_by_the_lookup()
    {
        var office = await OfficeAsync();
        await CreateCaseAsync(office, Case(office));

        var (takenStatus, taken) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases", Case(office));
        var (invalidStatus, invalid) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases", Case(office, cnj: "2238058-99.2025.8.26.8434"));
        var (missingStatus, missing) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases", Case(office, cnj: null));
        var (lookupStatus, lookup) = await SendAsync(office, HttpMethod.Get, "/api/v1/cases/cnj-lookup?number=22380589820258268434");

        takenStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(taken).ShouldBe("litigation.cnj_taken");
        invalidStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(invalid).ShouldBe("litigation.cnj_invalid");
        missingStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(missing).ShouldBe("litigation.cnj_required");
        lookupStatus.ShouldBe(HttpStatusCode.OK);
        lookup.GetProperty("formatted").GetString().ShouldBe(TjspCnj);
        lookup.GetProperty("year").GetInt32().ShouldBe(2025);
        lookup.GetProperty("court").GetProperty("acronym").GetString().ShouldBe("TJSP");

        var other = await OfficeAsync();
        (await SendAsync(other, HttpMethod.Post, "/api/v1/cases", Case(other))).Status.ShouldBe(HttpStatusCode.Created, "outro escritório pode ter o mesmo número");
    }

    [Fact]
    public async Task Parties_and_responsibles_must_belong_to_the_office()
    {
        var office = await OfficeAsync();
        var stranger = await OfficeAsync();

        var (personStatus, person) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases", Case(stranger));
        var (memberStatus, member) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases",
            Case(office, assignments: [new { role = "partner", userId = stranger.User }]));
        var (teamStatus, team) = await SendAsync(office, HttpMethod.Post, "/api/v1/cases",
            Case(office, assignments: [new { role = "team", teamId = Guid.CreateVersion7() }]));

        personStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(person).ShouldBe("litigation.person_not_found");
        memberStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(member).ShouldBe("litigation.responsible_invalid");
        teamStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(team).ShouldBe("litigation.team_invalid");
    }

    [Fact]
    public async Task A_closed_case_keeps_the_outcome_until_it_is_reopened()
    {
        var office = await OfficeAsync(seedLookups: true);
        var created = await CreateCaseAsync(office, Case(office));
        var id = IdOf(created);
        var (_, tables) = await SendAsync(office, HttpMethod.Get, "/api/v1/lookup-tables");
        var reason = tables.EnumerateArray()
            .Single(t => t.GetProperty("systemKey").ValueKind == JsonValueKind.String && t.GetProperty("systemKey").GetString() == SystemLookupTables.ClosingReasons)
            .GetProperty("items")[0].GetProperty("id").GetGuid();

        var (closeStatus, closed) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{id}/close",
            new { reasonId = reason, outcome = "settlement", finalAmount = 40_000m, closedOn = "2026-09-01", note = "Acordo homologado" });
        var (editStatus, edit) = await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{id}", Case(office));
        var (wrongReasonStatus, _) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{id}/close",
            new { reasonId = Guid.CreateVersion7(), outcome = "denied", closedOn = "2026-09-01" });
        var (reopenStatus, reopened) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{id}/reopen");

        closeStatus.ShouldBe(HttpStatusCode.OK, closed.ToString());
        closed.GetProperty("status").GetString().ShouldBe("closed");
        closed.GetProperty("outcome").GetString().ShouldBe("settlement");
        closed.GetProperty("finalAmount").GetDecimal().ShouldBe(40_000m);
        editStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(edit).ShouldBe("litigation.case_closed");
        wrongReasonStatus.ShouldBe(HttpStatusCode.BadRequest);
        reopenStatus.ShouldBe(HttpStatusCode.OK);
        reopened.GetProperty("status").GetString().ShouldBe("active");
        reopened.GetProperty("outcome").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_hierarchy_links_incidents_and_appeals_without_cycles()
    {
        var office = await OfficeAsync();
        var principal = IdOf(await CreateCaseAsync(office, Case(office)));
        var appeal = IdOf(await CreateCaseAsync(office, Case(office, cnj: Trf1Cnj)));
        var incident = IdOf(await CreateCaseAsync(office, Case(office, cnj: null, kind: "extrajudicial")));

        var (linkStatus, linked) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{principal}/relations", new { childCaseId = appeal, kind = "appeal" });
        await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{appeal}/relations", new { childCaseId = incident, kind = "incident" });
        var (cycleStatus, cycle) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{incident}/relations", new { childCaseId = principal, kind = "attached" });
        var (secondParentStatus, secondParent) = await SendAsync(office, HttpMethod.Post, $"/api/v1/cases/{principal}/relations", new { childCaseId = incident, kind = "incident" });
        var (_, middle) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{appeal}");
        var relation = middle.GetProperty("parent").GetProperty("relationId").GetGuid();
        var (unlinkStatus, _) = await SendAsync(office, HttpMethod.Delete, $"/api/v1/cases/{appeal}/relations/{relation}");
        var (_, detached) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{appeal}");

        linkStatus.ShouldBe(HttpStatusCode.OK, linked.ToString());
        linked.GetProperty("children")[0].GetProperty("caseId").GetGuid().ShouldBe(appeal);
        cycleStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(cycle).ShouldBe("litigation.relation_cycle");
        secondParentStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(secondParent).ShouldBe("litigation.relation_exists");
        middle.GetProperty("parent").GetProperty("caseId").GetGuid().ShouldBe(principal);
        middle.GetProperty("children")[0].GetProperty("kind").GetString().ShouldBe("incident");
        unlinkStatus.ShouldBe(HttpStatusCode.NoContent);
        detached.GetProperty("parent").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    /// <summary>
    /// Critério de aceite da Etapa 4 com processos reais: "conflito detecta parte contrária cadastrada em processo de
    /// outro cliente". A "Construtora Rival" é só testemunha no cadastro; o conflito vem do processo.
    /// </summary>
    [Fact]
    public async Task The_conflict_check_finds_an_opponent_in_a_case_of_another_client()
    {
        var office = await OfficeAsync();
        var created = await CreateCaseAsync(office, Case(office));

        var (status, check) = await SendAsync(office, HttpMethod.Post, "/api/v1/conflict-checks", new { name = "Construtora Rival Ltda.", side = "client" });

        status.ShouldBe(HttpStatusCode.Created, check.ToString());
        check.GetProperty("risk").GetString().ShouldBe("direct");
        var hit = check.GetProperty("hits").EnumerateArray().Single(h => h.GetProperty("reason").GetString() == "caseParty");
        hit.GetProperty("caseNumber").GetString().ShouldBe(TjspCnj);
        hit.GetProperty("matchedName").GetString().ShouldBe("Construtora Rival Ltda. (processo de Banco Alfa S.A.)");
        created.GetProperty("cnjNumber").GetString().ShouldBe(TjspCnj);
    }

    [Fact]
    public async Task The_office_numbering_format_and_area_codes_are_used_from_then_on()
    {
        var office = await OfficeAsync();
        var area = await _api.CreateAsync("/api/v1/organization/practice-areas", office.User, office.Organization, new { name = "Direito Tributário" });

        var (configStatus, config) = await SendAsync(office, HttpMethod.Put, "/api/v1/cases/numbering",
            new { format = "{ANO}/{AREA}/{SEQ:000}", areaCodes = new Dictionary<Guid, string> { [area] = "TRIB" } });
        var (invalidStatus, invalid) = await SendAsync(office, HttpMethod.Put, "/api/v1/cases/numbering", new { format = "{AREA}-{ANO}" });
        var created = await CreateCaseAsync(office, Case(office, area: area));

        configStatus.ShouldBe(HttpStatusCode.OK, config.ToString());
        config.GetProperty("example").GetString().ShouldBe($"{Year}/TRAB/001");
        invalidStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(invalid).ShouldBe("litigation.numbering_format_invalid");
        created.GetProperty("internalFolder").GetString().ShouldBe($"{Year}/TRIB/001");
    }

    [Fact]
    public async Task Merging_people_in_the_crm_points_the_parties_to_the_kept_record()
    {
        var office = await OfficeAsync();
        var kept = await _api.CreateAsync("/api/v1/people", office.User, office.Organization, new { kind = "company", name = "Banco Alfa", roles = ClientRole });
        var created = await CreateCaseAsync(office, Case(office));

        using (AmbientContext.Begin(office.Organization, "case-tests"))
        await using (var provider = dependencies.PlatformServices())
        await using (var scope = provider.CreateAsyncScope())
        {
            var consumer = ActivatorUtilities.CreateInstance<PeopleMergedConsumer>(scope.ServiceProvider);
            await consumer.Handle(new PeopleMergedIntegrationEvent(kept, office.Client) { OrganizationId = office.Organization }, Ct);
        }

        var (_, fetched) = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{IdOf(created)}");
        fetched.GetProperty("parties").EnumerateArray().Single(p => p.GetProperty("isClient").GetBoolean())
            .GetProperty("personId").GetGuid().ShouldBe(kept);
    }
}

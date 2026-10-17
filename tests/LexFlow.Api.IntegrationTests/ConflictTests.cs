using System.Net;
using System.Text.Json;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Contracts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Conflito de interesses (RF-07), fusão de duplicados e anonimização LGPD (Etapa 4, PR 4B) pela API real. As partes
/// de processos vêm de um <see cref="ILitigationLookup"/> falso (decisão de 2026-10-17: os processos reais chegam na
/// Etapa 5).
/// </summary>
public sealed class ConflictTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly string[] Everything =
    [
        "crm.people.read", "crm.people.create", "crm.people.edit", "crm.people.delete", "crm.people.merge", "crm.people.anonymize", "crm.conflicts.check",
    ];

    private static readonly string[] ClientRole = ["client"];
    private static readonly string[] OpponentRole = ["opponent"];

    private static readonly string[] BetaContact = ["CONTATO@beta.com.br"];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    /// <summary>Processos falsos: a "Construtora Rival" é parte contrária num processo do cliente "Banco Alfa".</summary>
    private sealed class FakeLitigation : ILitigationLookup
    {
        public Task<IReadOnlyList<CaseParty>> FindPartiesAsync(string searchName, string? document, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseParty>>(string.Equals(searchName, "construtora rival", StringComparison.Ordinal) || string.Equals(document, "11444777000161", StringComparison.Ordinal)
                ? [new CaseParty(Guid.CreateVersion7(), "0001234-56.2026.8.26.0100", "Construtora Rival Ltda.", "11444777000161", CasePartySide.Opponent, "Banco Alfa")]
                : []);
    }

    public ValueTask InitializeAsync()
    {
        _http = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<ILitigationLookup, FakeLitigation>())).CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private OrganizationApi Api => _api;

    private async Task<(Guid Organization, Guid User)> OfficeAsync()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies, Everything);
        return (member.OrganizationId, member.UserId);
    }

    private Task<Guid> PersonAsync(Guid organization, Guid user, string name, string[] roles, string? document = null, Guid? group = null, string? email = null) =>
        Api.CreateAsync("/api/v1/people", user, organization, new { kind = "company", name, document, roles, economicGroupId = group, email });

    private Task<(HttpStatusCode Status, JsonElement Body)> CheckAsync(Guid organization, Guid user, object body) =>
        Api.SendAsync(HttpMethod.Post, "/api/v1/conflict-checks", user, organization, body);

    private static (string? Risk, string? Reason) First(JsonElement check) =>
        (check.GetProperty("hits")[0].GetProperty("risk").GetString(), check.GetProperty("hits")[0].GetProperty("reason").GetString());

    [Fact]
    public async Task A_future_client_who_is_already_an_opponent_is_a_direct_conflict_that_needs_a_justification()
    {
        var (organization, user) = await OfficeAsync();
        await PersonAsync(organization, user, "Empresa Adversária S.A.", OpponentRole, "11222333000181");

        var (status, check) = await CheckAsync(organization, user, new { name = "Nome Qualquer", document = "11.222.333/0001-81", side = "client" });
        var id = check.GetProperty("id").GetGuid();
        var (missingStatus, missing) = await Api.SendAsync(HttpMethod.Post, $"/api/v1/conflict-checks/{id}/proceed", user, organization, new { justification = "" });
        var (proceedStatus, proceeded) = await Api.SendAsync(HttpMethod.Post, $"/api/v1/conflict-checks/{id}/proceed", user, organization, new { justification = "Causa sem relação com a anterior." });
        var (againStatus, _) = await Api.SendAsync(HttpMethod.Post, $"/api/v1/conflict-checks/{id}/proceed", user, organization, new { justification = "de novo" });

        status.ShouldBe(HttpStatusCode.Created, check.ToString());
        check.GetProperty("risk").GetString().ShouldBe("direct");
        First(check).ShouldBe(("direct", "sameDocument"));
        missingStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(missing).ShouldBe("crm.conflict_justification_required");
        proceedStatus.ShouldBe(HttpStatusCode.OK);
        proceeded.GetProperty("justification").GetString().ShouldBe("Causa sem relação com a anterior.");
        againStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task The_side_decides_which_existing_role_is_a_conflict()
    {
        var (organization, user) = await OfficeAsync();
        await PersonAsync(organization, user, "Cliente Antigo Ltda.", ClientRole);

        var (_, asOpponent) = await CheckAsync(organization, user, new { name = "Cliente Antigo Ltda", side = "opponent" });
        var (_, asClient) = await CheckAsync(organization, user, new { name = "Cliente Antigo Ltda", side = "client" });

        asOpponent.GetProperty("risk").GetString().ShouldBe("direct");
        First(asOpponent).ShouldBe(("direct", "sameName"));
        asClient.GetProperty("risk").GetString().ShouldBe("none", "já ser cliente não é conflito para virar cliente de novo");
    }

    /// <summary>Critério de aceite da Etapa 4 (com processos falsos até a Etapa 5): parte contrária num processo de outro cliente.</summary>
    [Fact]
    public async Task An_opponent_in_a_case_of_another_client_is_a_direct_conflict()
    {
        var (organization, user) = await OfficeAsync();

        var (_, check) = await CheckAsync(organization, user, new { name = "Construtora Rival", side = "client" });

        check.GetProperty("risk").GetString().ShouldBe("direct");
        var hit = check.GetProperty("hits")[0];
        hit.GetProperty("reason").GetString().ShouldBe("caseParty");
        hit.GetProperty("caseNumber").GetString().ShouldBe("0001234-56.2026.8.26.0100");
        hit.GetProperty("matchedName").GetString().ShouldNotBeNull().ShouldContain("Banco Alfa");
    }

    [Fact]
    public async Task Similar_names_economic_groups_and_shared_contacts_are_indirect_conflicts()
    {
        var (organization, user) = await OfficeAsync();
        var group = await Api.CreateAsync("/api/v1/economic-groups", user, organization, new { name = "Grupo Horizonte" });
        await PersonAsync(organization, user, "Horizonte Construções Ltda.", ClientRole, group: group);
        await PersonAsync(organization, user, "Horizonte Incorporadora S.A.", OpponentRole, group: group);
        await PersonAsync(organization, user, "Transportes Velozes Ltda.", OpponentRole);
        await PersonAsync(organization, user, "Fornecedora Beta", OpponentRole, email: "contato@beta.com.br");

        var (_, sameGroup) = await CheckAsync(organization, user, new { name = "Horizonte Construções Ltda.", side = "client" });
        var (_, similar) = await CheckAsync(organization, user, new { name = "Transportes Veloz Ltda", side = "client" });
        var (_, shared) = await CheckAsync(organization, user, new { name = "Empresa Nova", side = "client", contactEmails = BetaContact });
        var (_, none) = await CheckAsync(organization, user, new { name = "Ninguém Parecido", side = "client" });
        var (proceedStatus, _) = await Api.SendAsync(HttpMethod.Post, $"/api/v1/conflict-checks/{none.GetProperty("id").GetGuid()}/proceed", user, organization, new { justification = (string?)null });

        sameGroup.GetProperty("risk").GetString().ShouldBe("indirect");
        First(sameGroup).ShouldBe(("indirect", "sameEconomicGroup"));
        First(similar).ShouldBe(("indirect", "similarName"));
        similar.GetProperty("hits")[0].GetProperty("score").GetDouble().ShouldBeGreaterThanOrEqualTo(0.6);
        First(shared).ShouldBe(("indirect", "sharedContact"));
        none.GetProperty("risk").GetString().ShouldBe("none");
        none.GetProperty("hits").GetArrayLength().ShouldBe(0);
        proceedStatus.ShouldBe(HttpStatusCode.OK, "sem conflito, não precisa de justificativa");
    }

    [Fact]
    public async Task The_report_lists_checks_by_risk_newest_first()
    {
        var (organization, user) = await OfficeAsync();
        await PersonAsync(organization, user, "Adversária Ltda.", OpponentRole, "11222333000181");
        await CheckAsync(organization, user, new { name = "Sem conflito 1", side = "client" });
        await CheckAsync(organization, user, new { name = "X", document = "11222333000181", side = "client" });
        await CheckAsync(organization, user, new { name = "Sem conflito 2", side = "client" });

        var (_, direct) = await Api.SendAsync(HttpMethod.Get, "/api/v1/conflict-checks?risk=direct", user, organization);
        var (_, first) = await Api.SendAsync(HttpMethod.Get, "/api/v1/conflict-checks?limit=2", user, organization);
        var (_, second) = await Api.SendAsync(HttpMethod.Get, $"/api/v1/conflict-checks?limit=2&cursor={first.GetProperty("nextCursor").GetString()}", user, organization);

        direct.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("queryName").GetString()).ShouldBe(["X"]);
        first.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("queryName").GetString()).ShouldBe(["Sem conflito 2", "X"]);
        second.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("queryName").GetString()).ShouldBe(["Sem conflito 1"]);
        second.GetProperty("items")[0].GetProperty("checkedBy").GetGuid().ShouldBe(user);
    }

    [Fact]
    public async Task Merging_moves_data_and_conflict_hits_to_the_kept_record()
    {
        var (organization, user) = await OfficeAsync();
        var keep = await PersonAsync(organization, user, "Horizonte S.A.", ClientRole);
        var duplicate = await PersonAsync(organization, user, "Horizonte SA", OpponentRole, "11222333000181", email: "contato@horizonte.com.br");
        var (_, check) = await CheckAsync(organization, user, new { name = "Qualquer", document = "11222333000181", side = "client" });

        var (status, merged) = await Api.SendAsync(HttpMethod.Post, $"/api/v1/people/{keep}/merge", user, organization, new { duplicateId = duplicate });
        var (goneStatus, _) = await Api.SendAsync(HttpMethod.Get, $"/api/v1/people/{duplicate}", user, organization);
        var (_, after) = await Api.SendAsync(HttpMethod.Get, $"/api/v1/conflict-checks/{check.GetProperty("id").GetGuid()}", user, organization);
        var (selfStatus, _) = await Api.SendAsync(HttpMethod.Post, $"/api/v1/people/{keep}/merge", user, organization, new { duplicateId = keep });

        status.ShouldBe(HttpStatusCode.OK, merged.ToString());
        merged.GetProperty("document").GetString().ShouldBe("11222333000181");
        merged.GetProperty("email").GetString().ShouldBe("contato@horizonte.com.br");
        merged.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["client", "opponent"]);
        goneStatus.ShouldBe(HttpStatusCode.NotFound);
        after.GetProperty("hits")[0].GetProperty("personId").GetGuid().ShouldBe(keep);
        selfStatus.ShouldBe(HttpStatusCode.BadRequest);
        _factory.PublishedEvents.OfType<PeopleMergedIntegrationEvent>().ShouldContain(e => e.KeptPersonId == keep && e.MergedPersonId == duplicate);
    }

    [Fact]
    public async Task Anonymizing_removes_personal_data_everywhere_and_freezes_the_record()
    {
        var (organization, user) = await OfficeAsync();
        var person = await PersonAsync(organization, user, "João da Silva Ltda.", OpponentRole, "11222333000181", email: "joao@x.com.br");
        var (_, check) = await CheckAsync(organization, user, new { name = "João da Silva Ltda.", document = "11222333000181", side = "client" });

        var (noReasonStatus, _) = await Api.SendAsync(HttpMethod.Post, $"/api/v1/people/{person}/anonymize", user, organization, new { reason = "" });
        var (status, anonymized) = await Api.SendAsync(HttpMethod.Post, $"/api/v1/people/{person}/anonymize", user, organization, new { reason = "Pedido do titular (art. 18 da LGPD)" });
        var (_, after) = await Api.SendAsync(HttpMethod.Get, $"/api/v1/conflict-checks/{check.GetProperty("id").GetGuid()}", user, organization);
        var (editStatus, edit) = await Api.SendAsync(HttpMethod.Put, $"/api/v1/people/{person}", user, organization, new { name = "Volta", roles = ClientRole });
        var (_, search) = await Api.SendAsync(HttpMethod.Get, "/api/v1/people?q=Joao%20da%20Silva", user, organization);

        noReasonStatus.ShouldBe(HttpStatusCode.BadRequest);
        status.ShouldBe(HttpStatusCode.OK, anonymized.ToString());
        anonymized.GetProperty("name").GetString().ShouldStartWith("Pessoa anonimizada");
        anonymized.GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Null);
        anonymized.GetProperty("email").ValueKind.ShouldBe(JsonValueKind.Null);
        anonymized.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["opponent"]);
        after.GetProperty("queryName").GetString().ShouldStartWith("Pessoa anonimizada");
        after.GetProperty("queryDocument").ValueKind.ShouldBe(JsonValueKind.Null);
        after.GetProperty("hits")[0].GetProperty("matchedName").GetString().ShouldStartWith("Pessoa anonimizada");
        editStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(edit).ShouldBe("crm.person_anonymized");
        search.GetProperty("items").GetArrayLength().ShouldBe(0);
        _factory.PublishedEvents.OfType<PersonAnonymizedIntegrationEvent>().ShouldContain(e => e.PersonId == person);
    }
}

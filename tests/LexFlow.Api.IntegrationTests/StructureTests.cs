using System.Net;
using LexFlow.Organization.Application.Onboarding;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Cadastro da estrutura (seção 3.1, RF-02) pela API real: filiais, áreas, equipes, centros de custo e sócios.</summary>
public sealed class StructureTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private const string Base = "/api/v1/organization";

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        _api = new OrganizationApi(_client);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Branches_are_created_edited_and_listed_with_unique_names_and_valid_cnpj()
    {
        var (organization, admin) = await _api.OnboardAsync();

        var branch = await _api.CreateAsync($"{Base}/branches", admin, organization, new { name = "Matriz São Paulo", cnpj = "11.222.333/0001-81", municipalRegistration = "123" });
        var (invalidStatus, invalid) = await _api.SendAsync(HttpMethod.Post, $"{Base}/branches", admin, organization, new { name = "Filial Rio", cnpj = "11.222.333/0001-82" });
        var (takenStatus, taken) = await _api.SendAsync(HttpMethod.Post, $"{Base}/branches", admin, organization, new { name = " Matriz São Paulo " });
        var (updateStatus, updated) = await _api.SendAsync(HttpMethod.Put, $"{Base}/branches/{branch}", admin, organization, new { name = "Matriz SP", cnpj = (string?)null, municipalRegistration = "" });
        var (_, list) = await _api.SendAsync(HttpMethod.Get, $"{Base}/branches", admin, organization);

        invalidStatus.ShouldBe(HttpStatusCode.BadRequest);
        invalid.GetProperty("errors").GetProperty("cnpj")[0].GetProperty("code").GetString().ShouldBe("organization.cnpj_invalid");
        takenStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(taken).ShouldBe("organization.name_taken");
        updateStatus.ShouldBe(HttpStatusCode.OK);
        updated.GetProperty("cnpj").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        updated.GetProperty("municipalRegistration").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        list.EnumerateArray().Select(b => b.GetProperty("name").GetString()).ShouldBe(["Matriz SP"]);
    }

    [Fact]
    public async Task Teams_reference_the_structure_of_the_office_and_hold_its_members()
    {
        var (organization, admin) = await _api.OnboardAsync();
        var other = await _api.OnboardAsync("Outro escritório");
        var area = await _api.CreateAsync($"{Base}/practice-areas", admin, organization, new { name = "Trabalhista" });
        var foreignArea = await _api.CreateAsync($"{Base}/practice-areas", other.Admin, other.Organization, new { name = "Cível" });
        var member = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7());

        var team = await _api.CreateAsync($"{Base}/teams", admin, organization, new { name = "Equipe A", practiceAreaId = area });
        var (foreignStatus, foreign) = await _api.SendAsync(HttpMethod.Post, $"{Base}/teams", admin, organization, new { name = "Equipe B", practiceAreaId = foreignArea });
        var (membersStatus, withMembers) = await _api.SendAsync(HttpMethod.Put, $"{Base}/teams/{team}/members", admin, organization, new { memberIds = new[] { member.MemberId } });
        var (strangerStatus, _) = await _api.SendAsync(HttpMethod.Put, $"{Base}/teams/{team}/members", admin, organization, new { memberIds = new[] { Guid.CreateVersion7() } });
        var (areaInUseStatus, areaInUse) = await _api.SendAsync(HttpMethod.Delete, $"{Base}/practice-areas/{area}", admin, organization);

        foreignStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(foreign).ShouldBe("organization.structure_reference_not_found");
        membersStatus.ShouldBe(HttpStatusCode.OK);
        withMembers.GetProperty("memberIds").EnumerateArray().Select(m => m.GetGuid()).ShouldBe([member.MemberId]);
        strangerStatus.ShouldBe(HttpStatusCode.NotFound);
        areaInUseStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(areaInUse).ShouldBe("organization.structure_in_use");

        (await _api.SendAsync(HttpMethod.Delete, $"{Base}/teams/{team}", admin, organization)).Status.ShouldBe(HttpStatusCode.NoContent);
        (await _api.SendAsync(HttpMethod.Delete, $"{Base}/practice-areas/{area}", admin, organization)).Status.ShouldBe(HttpStatusCode.NoContent);
        (await _api.SendAsync(HttpMethod.Get, $"{Base}/teams", admin, organization)).Body.GetArrayLength().ShouldBe(0);
        (await _api.SendAsync(HttpMethod.Delete, $"{Base}/teams/{team}", admin, organization)).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_team_used_as_a_data_scope_cannot_be_deleted()
    {
        var (organization, admin) = await _api.OnboardAsync();
        var team = await _api.CreateAsync($"{Base}/teams", admin, organization, new { name = "Contencioso" });
        var member = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7());
        var lawyer = await _api.RoleIdAsync(organization, admin, DefaultRoles.Lawyer);
        (await _api.SendAsync(HttpMethod.Put, $"{Base}/members/{member.MemberId}/roles", admin, organization, new { roles = new[] { new { roleId = lawyer, scope = "team", scopeRefId = team } } }))
            .Status.ShouldBe(HttpStatusCode.NoContent);

        var (status, problem) = await _api.SendAsync(HttpMethod.Delete, $"{Base}/teams/{team}", admin, organization);

        status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(problem).ShouldBe("organization.structure_in_use");
    }

    [Fact]
    public async Task Cost_center_codes_are_unique_and_partner_shares_never_pass_100_percent()
    {
        var (organization, admin) = await _api.OnboardAsync();
        var member = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7());

        var (createdStatus, costCenter) = await _api.SendAsync(HttpMethod.Post, $"{Base}/cost-centers", admin, organization, new { code = " adm-01 ", name = "Administrativo" });
        var (codeStatus, code) = await _api.SendAsync(HttpMethod.Post, $"{Base}/cost-centers", admin, organization, new { code = "ADM-01", name = "Outro" });
        var first = await _api.CreateAsync($"{Base}/partners", admin, organization, new { name = "Ana Souza", sharePercentage = 60m, memberId = member.MemberId });
        var (overStatus, over) = await _api.SendAsync(HttpMethod.Post, $"{Base}/partners", admin, organization, new { name = "Bruno Lima", sharePercentage = 40.0001m });
        var (shareStatus, share) = await _api.SendAsync(HttpMethod.Post, $"{Base}/partners", admin, organization, new { name = "Carla Dias", sharePercentage = 0m });
        var (editStatus, _) = await _api.SendAsync(HttpMethod.Put, $"{Base}/partners/{first}", admin, organization, new { name = "Ana Souza", sharePercentage = 59.5m, memberId = member.MemberId });
        var second = await _api.CreateAsync($"{Base}/partners", admin, organization, new { name = "Bruno Lima", sharePercentage = 40.5m });

        createdStatus.ShouldBe(HttpStatusCode.Created);
        costCenter.GetProperty("code").GetString().ShouldBe("ADM-01");
        codeStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(code).ShouldBe("organization.code_taken");
        overStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(over).ShouldBe("organization.partner_shares_exceed_total");
        shareStatus.ShouldBe(HttpStatusCode.BadRequest);
        share.GetProperty("errors").GetProperty("sharePercentage")[0].GetProperty("code").GetString().ShouldBe("organization.invalid_partner_share");
        editStatus.ShouldBe(HttpStatusCode.OK);
        (await _api.SendAsync(HttpMethod.Get, $"{Base}/partners", admin, organization)).Body.EnumerateArray()
            .Select(p => p.GetProperty("sharePercentage").GetDecimal()).ShouldBe([59.5m, 40.5m]);
        (await _api.SendAsync(HttpMethod.Delete, $"{Base}/partners/{second}", admin, organization)).Status.ShouldBe(HttpStatusCode.NoContent);
        (await _api.SendAsync(HttpMethod.Delete, $"{Base}/cost-centers/{costCenter.GetProperty("id").GetGuid()}", admin, organization)).Status.ShouldBe(HttpStatusCode.NoContent);
        (await _api.SendAsync(HttpMethod.Post, $"{Base}/cost-centers", admin, organization, new { code = "ADM-01", name = "De novo" })).Status.ShouldBe(HttpStatusCode.Created);
    }
}

using System.Net;
using System.Text.Json;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Cadastro único de pessoas (RF-06, Etapa 4, PR 4A) pela API real, com PostgreSQL e a consulta de CNPJ falsa.</summary>
public sealed class PeopleTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly string[] Everything =
    [
        "crm.people.read", "crm.people.create", "crm.people.edit", "crm.people.delete", "settings.custom_fields.read", "settings.custom_fields.manage",
    ];

    private static readonly string[] ClientRole = ["client"];
    private static readonly string[] OpponentRole = ["opponent"];

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

    private static object Company(string name, string? document = null, string[]? roles = null, object? customFields = null) => new
    {
        kind = "company",
        name,
        document,
        roles = roles ?? ["client"],
        addresses = new[] { new { street = "Av. Paulista", number = "1000", city = "São Paulo", state = "SP", postalCode = "01310-100", isBilling = true } },
        contacts = new[] { new { name = "Carla Financeiro", department = "Financeiro", email = "carla@exemplo.com.br", preferredChannel = "email" } },
        fiscal = new { stateRegistration = "123456", withholdsIss = true },
        customFields,
    };

    private async Task<(Guid Organization, Guid User)> OfficeAsync()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies, Everything);
        return (member.OrganizationId, member.UserId);
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> CreateAsync(Guid organization, Guid user, object body) =>
        _api.SendAsync(HttpMethod.Post, "/api/v1/people", user, organization, body);

    [Fact]
    public async Task A_company_is_created_with_roles_addresses_contacts_and_fiscal_data()
    {
        var (organization, user) = await OfficeAsync();

        var (status, created) = await CreateAsync(organization, user, Company("João da Silva Ltda.", "11.222.333/0001-81", ["client", "supplier"]));
        var (getStatus, person) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/people/{created.GetProperty("id").GetGuid()}", user, organization);

        status.ShouldBe(HttpStatusCode.Created, created.ToString());
        getStatus.ShouldBe(HttpStatusCode.OK);
        person.GetProperty("document").GetString().ShouldBe("11222333000181");
        person.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["client", "supplier"]);
        person.GetProperty("addresses")[0].GetProperty("postalCode").GetString().ShouldBe("01310100");
        person.GetProperty("contacts")[0].GetProperty("department").GetString().ShouldBe("Financeiro");
        person.GetProperty("fiscal").GetProperty("withholdsIss").GetBoolean().ShouldBeTrue();
    }

    /// <summary>Critério de aceite da Etapa 4: CPF/CNPJ duplicado no mesmo escritório é bloqueado; em outro, é permitido.</summary>
    [Fact]
    public async Task A_duplicate_document_is_refused_in_the_same_office_and_allowed_in_another()
    {
        var (organization, user) = await OfficeAsync();
        var (otherOrganization, otherUser) = await OfficeAsync();
        await CreateAsync(organization, user, Company("Empresa A", "11222333000181"));

        var (duplicateStatus, duplicate) = await CreateAsync(organization, user, Company("Empresa A de novo", "11.222.333/0001-81"));
        var (otherStatus, _) = await CreateAsync(otherOrganization, otherUser, Company("Empresa A", "11222333000181"));
        var (invalidStatus, invalid) = await CreateAsync(organization, user, Company("Empresa B", "11222333000182"));

        duplicateStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(duplicate).ShouldBe("crm.document_taken");
        otherStatus.ShouldBe(HttpStatusCode.Created);
        invalidStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(invalid).ShouldBe("crm.cnpj_invalid");
    }

    /// <summary>Critério de aceite da Etapa 4: busca por "Joao da Silva" encontra "João da Silva Ltda.".</summary>
    [Fact]
    public async Task Searching_without_accents_finds_the_longer_name_and_documents_are_searchable()
    {
        var (organization, user) = await OfficeAsync();
        await CreateAsync(organization, user, Company("João da Silva Ltda.", "11222333000181"));
        await CreateAsync(organization, user, Company("Maria Souza Advocacia"));

        var (status, page) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people?q=Joao%20da%20Silva", user, organization);
        var (_, byDocument) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people?q=11.222.333", user, organization);
        var (_, all) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people?limit=1", user, organization);
        var (_, next) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/people?limit=1&cursor={all.GetProperty("nextCursor").GetString()}", user, organization);

        status.ShouldBe(HttpStatusCode.OK);
        var found = page.GetProperty("items").EnumerateArray().ToList();
        found.Select(p => p.GetProperty("name").GetString()).ShouldBe(["João da Silva Ltda."]);
        found[0].GetProperty("score").GetDouble().ShouldBe(1, 0.001);
        byDocument.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("João da Silva Ltda.");
        all.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("João da Silva Ltda.");
        next.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Maria Souza Advocacia");
        next.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_list_filters_by_role_written_in_camel_case()
    {
        var (organization, user) = await OfficeAsync();
        await CreateAsync(organization, user, Company("Cliente Um"));
        await CreateAsync(organization, user, Company("Adversária Um", roles: OpponentRole));

        var (_, opponents) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people?role=opponent", user, organization);
        var (invalidStatus, invalid) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people?role=juiz", user, organization);

        opponents.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ShouldBe(["Adversária Um"]);
        invalidStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(invalid).ShouldBe("crm.filter_invalid");
    }

    [Fact]
    public async Task Similar_names_and_documents_are_reported_as_possible_duplicates()
    {
        var (organization, user) = await OfficeAsync();
        var (_, existing) = await CreateAsync(organization, user, Company("Construtora Horizonte S.A.", "11222333000181"));

        var (_, byName) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people/similar?name=Construtora%20Horizonte%20SA", user, organization);
        var (_, byDocument) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people/similar?document=11222333000181", user, organization);
        var (_, itself) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/people/similar?document=11222333000181&excludeId={existing.GetProperty("id").GetGuid()}", user, organization);

        byName[0].GetProperty("name").GetString().ShouldBe("Construtora Horizonte S.A.");
        byName[0].GetProperty("sameDocument").GetBoolean().ShouldBeFalse();
        byDocument[0].GetProperty("sameDocument").GetBoolean().ShouldBeTrue();
        itself.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Custom_fields_of_people_are_validated_and_stored()
    {
        var (organization, user) = await OfficeAsync();
        (await _api.SendAsync(HttpMethod.Post, "/api/v1/custom-fields", user, organization, new { entity = "people", key = "origem", type = "text", label = "Origem", required = true }))
            .Status.ShouldBe(HttpStatusCode.Created);

        var (missingStatus, missing) = await CreateAsync(organization, user, Company("Sem origem"));
        var (createdStatus, created) = await CreateAsync(organization, user, Company("Com origem", customFields: new { origem = " Indicação " }));

        missingStatus.ShouldBe(HttpStatusCode.BadRequest);
        missing.GetProperty("errors").GetProperty("customFields.origem")[0].GetProperty("code").GetString().ShouldBe("custom_fields.required");
        createdStatus.ShouldBe(HttpStatusCode.Created);
        created.GetProperty("customFields").GetProperty("origem").GetString().ShouldBe("Indicação");
    }

    [Fact]
    public async Task People_are_updated_and_deleted_and_the_document_is_released()
    {
        var (organization, user) = await OfficeAsync();
        var (_, created) = await CreateAsync(organization, user, Company("Empresa Antiga", "11222333000181"));
        var id = created.GetProperty("id").GetGuid();

        var (updateStatus, updated) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/people/{id}", user, organization, new
        {
            name = "Empresa Nova",
            alternateName = "Nova",
            document = "11222333000181",
            roles = OpponentRole,
        });
        var (deleteStatus, _) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/people/{id}", user, organization);
        var (afterStatus, _) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/people/{id}", user, organization);
        var (againStatus, _) = await CreateAsync(organization, user, Company("Empresa Recadastrada", "11222333000181"));

        updateStatus.ShouldBe(HttpStatusCode.OK, updated.ToString());
        updated.GetProperty("name").GetString().ShouldBe("Empresa Nova");
        updated.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(["opponent"]);
        updated.GetProperty("addresses").GetArrayLength().ShouldBe(0);
        updated.GetProperty("kind").GetString().ShouldBe("company");
        deleteStatus.ShouldBe(HttpStatusCode.NoContent);
        afterStatus.ShouldBe(HttpStatusCode.NotFound);
        againStatus.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>Regressão (PR 5B): endereço e contato novos numa edição são inseridos (o id vem do domínio).</summary>
    [Fact]
    public async Task An_update_adds_new_addresses_and_contacts()
    {
        var (organization, user) = await OfficeAsync();
        var (_, created) = await CreateAsync(organization, user, Company("Empresa com Sede"));
        var id = created.GetProperty("id").GetGuid();

        var (status, updated) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/people/{id}", user, organization, new
        {
            name = "Empresa com Sede",
            roles = ClientRole,
            addresses = new[] { new { street = "Av. Paulista", number = "1000", city = "São Paulo", state = "SP", postalCode = "01310-100", isBilling = true } },
            contacts = new[] { new { name = "Ana", email = "ana@sede.com.br", preferredChannel = "email" } },
        });

        status.ShouldBe(HttpStatusCode.OK, updated.ToString());
        updated.GetProperty("addresses")[0].GetProperty("street").GetString().ShouldBe("Av. Paulista");
        updated.GetProperty("contacts")[0].GetProperty("name").GetString().ShouldBe("Ana");
    }

    [Fact]
    public async Task The_cnpj_lookup_fills_in_the_company_or_explains_why_not()
    {
        var (organization, user) = await OfficeAsync();

        var (status, company) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people/cnpj-lookup?cnpj=11.222.333/0001-81", user, organization);
        var (invalidStatus, _) = await _api.SendAsync(HttpMethod.Get, "/api/v1/people/cnpj-lookup?cnpj=123", user, organization);

        status.ShouldBe(HttpStatusCode.OK);
        company.GetProperty("cnpj").GetString().ShouldBe("11222333000181");
        company.GetProperty("legalName").GetString().ShouldBe("Empresa 11222333 Ltda.");
        company.GetProperty("address").GetProperty("state").GetString().ShouldBe("SP");
        invalidStatus.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Economic_groups_gather_people_and_cannot_be_deleted_while_in_use()
    {
        var (organization, user) = await OfficeAsync();
        var group = await _api.CreateAsync("/api/v1/economic-groups", user, organization, new { name = "Grupo Horizonte" });
        var (duplicateStatus, _) = await _api.SendAsync(HttpMethod.Post, "/api/v1/economic-groups", user, organization, new { name = "Grupo Horizonte" });
        await CreateAsync(organization, user, new { kind = "company", name = "Horizonte Matriz", roles = ClientRole, economicGroupId = group });
        var (foreignGroupStatus, foreignGroup) = await CreateAsync(organization, user, new { kind = "company", name = "Outra", roles = ClientRole, economicGroupId = Guid.CreateVersion7() });

        var (_, groups) = await _api.SendAsync(HttpMethod.Get, "/api/v1/economic-groups", user, organization);
        var (inUseStatus, inUse) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/economic-groups/{group}", user, organization);

        duplicateStatus.ShouldBe(HttpStatusCode.Conflict);
        foreignGroupStatus.ShouldBe(HttpStatusCode.NotFound);
        OrganizationApi.Code(foreignGroup).ShouldBe("crm.economic_group_not_found");
        groups[0].GetProperty("memberCount").GetInt32().ShouldBe(1);
        inUseStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(inUse).ShouldBe("crm.economic_group_in_use");
    }
}

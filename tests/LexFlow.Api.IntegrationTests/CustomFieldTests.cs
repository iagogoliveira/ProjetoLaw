using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Infrastructure.CustomFields;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Campos customizados e tabelas auxiliares (RF-03, Etapa 3, PR 3E) pela API real: tabelas do sistema criadas pelo
/// Worker no onboarding, definições por entidade e validação dos valores.
/// </summary>
public sealed class CustomFieldTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;
    private OrganizationApi _api = null!;

    private sealed class NoEmail : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
    }

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

    /// <summary>Onboarding real e, no papel do Worker, o consumo do evento que cria as tabelas auxiliares.</summary>
    private async Task<(Guid Organization, Guid Admin)> OfficeAsync(bool deliverTwice = false)
    {
        var (organization, admin) = await _api.OnboardAsync();
        var created = _factory.PublishedEvents.OfType<OrganizationCreatedIntegrationEvent>().Single(e => e.OrganizationId == organization);
        await using var worker = dependencies.WorkerServices(new NoEmail());
        for (var delivery = 0; delivery < (deliverTwice ? 2 : 1); delivery++)
        {
            await using var scope = worker.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OrganizationCreatedConsumer>().Handle(created with { EventId = Guid.CreateVersion7() }, Ct);
        }

        return (organization, admin);
    }

    private async Task<JsonElement> TablesAsync(Guid organization, Guid user) =>
        (await _api.SendAsync(HttpMethod.Get, "/api/v1/lookup-tables", user, organization)).Body;

    private static JsonElement System(JsonElement tables, string key) =>
        tables.EnumerateArray().Single(t => t.GetProperty("systemKey").ValueKind == JsonValueKind.String && t.GetProperty("systemKey").GetString() == key);

    private Task<(HttpStatusCode Status, JsonElement Body)> ValidateAsync(Guid organization, Guid user, object values) =>
        _api.SendAsync(HttpMethod.Post, "/api/v1/custom-fields/validate", user, organization, new { entity = CustomFieldEntities.Cases, values });

    private static string? FieldCode(JsonElement problem, string key) =>
        problem.GetProperty("errors").GetProperty($"customFields.{key}")[0].GetProperty("code").GetString();

    [Fact]
    public async Task Onboarding_creates_the_system_lookup_tables_once_with_suggested_items()
    {
        var (organization, admin) = await OfficeAsync(deliverTwice: true);

        var tables = await TablesAsync(organization, admin);

        tables.GetArrayLength().ShouldBe(6);
        var phases = System(tables, SystemLookupTables.CasePhases);
        phases.GetProperty("name").GetString().ShouldBe("Fases");
        phases.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ShouldContain("Conhecimento");
        phases.GetProperty("items")[0].GetProperty("position").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Required_field_created_by_the_office_is_enforced_on_validation()
    {
        var (organization, admin) = await OfficeAsync();
        var phases = System(await TablesAsync(organization, admin), SystemLookupTables.CasePhases);
        var phase = phases.GetProperty("items")[0].GetProperty("id").GetGuid();
        var otherTableItem = System(await TablesAsync(organization, admin), SystemLookupTables.TaskTypes).GetProperty("items")[0].GetProperty("id").GetGuid();

        await _api.CreateAsync("/api/v1/custom-fields", admin, organization, new
        {
            entity = "cases",
            key = "numero_pasta",
            type = "text",
            label = "Número da pasta",
            required = true,
            position = 1,
            settings = new { maxLength = 20 },
        });
        await _api.CreateAsync("/api/v1/custom-fields", admin, organization, new
        {
            entity = "cases",
            key = "fase_interna",
            type = "list",
            label = "Fase interna",
            position = 2,
            settings = new { lookupTableId = phases.GetProperty("id").GetGuid() },
        });
        await _api.CreateAsync("/api/v1/custom-fields", admin, organization, new { entity = "cases", key = "socio_responsavel", type = "user", label = "Sócio responsável", position = 3 });

        var (missingStatus, missing) = await ValidateAsync(organization, admin, new { fase_interna = otherTableItem, socio_responsavel = Guid.CreateVersion7() });
        var (validStatus, valid) = await ValidateAsync(organization, admin, new { numero_pasta = " P-2026-001 ", fase_interna = phase, socio_responsavel = admin });

        missingStatus.ShouldBe(HttpStatusCode.BadRequest);
        FieldCode(missing, "numero_pasta").ShouldBe("custom_fields.required");
        FieldCode(missing, "fase_interna").ShouldBe("custom_fields.unknown_option");
        FieldCode(missing, "socio_responsavel").ShouldBe("custom_fields.unknown_user");
        validStatus.ShouldBe(HttpStatusCode.OK, valid.ToString());
        valid.GetProperty("values").GetProperty("numero_pasta").GetString().ShouldBe("P-2026-001");

        var (_, fields) = await _api.SendAsync(HttpMethod.Get, "/api/v1/custom-fields?entity=cases", admin, organization);
        fields.EnumerateArray().Select(f => f.GetProperty("key").GetString()).ShouldBe(["numero_pasta", "fase_interna", "socio_responsavel"]);
        fields[0].GetProperty("type").GetString().ShouldBe("text");
        fields[0].GetProperty("settings").GetProperty("maxLength").GetInt32().ShouldBe(20);
    }

    [Fact]
    public async Task Fields_keep_key_entity_and_type_and_are_deactivated_instead_of_deleted()
    {
        var (organization, admin) = await OfficeAsync();
        var field = await _api.CreateAsync("/api/v1/custom-fields", admin, organization, new { entity = "people", key = "origem", type = "text", label = "Origem", required = true });

        var (takenStatus, taken) = await _api.SendAsync(HttpMethod.Post, "/api/v1/custom-fields", admin, organization, new { entity = "people", key = "origem", type = "number", label = "Outro" });
        var (entityStatus, entity) = await _api.SendAsync(HttpMethod.Post, "/api/v1/custom-fields", admin, organization, new { entity = "tarefas", key = "x", type = "text", label = "X" });
        var (listStatus, list) = await _api.SendAsync(HttpMethod.Post, "/api/v1/custom-fields", admin, organization, new { entity = "people", key = "lista", type = "list", label = "Lista" });
        var (updateStatus, updated) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/custom-fields/{field}", admin, organization, new { label = "Origem do cliente", required = true, isActive = false });
        var (afterStatus, _) = await _api.SendAsync(HttpMethod.Post, "/api/v1/custom-fields/validate", admin, organization, new { entity = "people", values = new { } });

        takenStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(taken).ShouldBe("platform.custom_field_key_taken");
        entityStatus.ShouldBe(HttpStatusCode.BadRequest);
        entity.GetProperty("errors").GetProperty("entity")[0].GetProperty("code").GetString().ShouldBe("platform.custom_field_unknown_entity");
        listStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(list).ShouldBe("platform.custom_field_lookup_table_required");
        updateStatus.ShouldBe(HttpStatusCode.OK);
        updated.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        updated.GetProperty("key").GetString().ShouldBe("origem");
        afterStatus.ShouldBe(HttpStatusCode.OK, "campo inativo deixa de ser obrigatório");
    }

    [Fact]
    public async Task Office_maintains_lookup_tables_and_items_without_deleting_them()
    {
        var (organization, admin) = await OfficeAsync();
        var system = System(await TablesAsync(organization, admin), SystemLookupTables.ExpenseTypes).GetProperty("id").GetGuid();

        var table = await _api.CreateAsync("/api/v1/lookup-tables", admin, organization, new { name = "Origem do cliente" });
        var first = await _api.CreateAsync($"/api/v1/lookup-tables/{table}/items", admin, organization, new { name = "Indicação" });
        var (_, second) = await _api.SendAsync(HttpMethod.Post, $"/api/v1/lookup-tables/{table}/items", admin, organization, new { name = "Site" });
        var (duplicateStatus, duplicate) = await _api.SendAsync(HttpMethod.Post, $"/api/v1/lookup-tables/{table}/items", admin, organization, new { name = "indicação" });
        var (itemStatus, item) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/lookup-items/{first}", admin, organization, new { name = "Indicação de cliente", position = 5, isActive = false });
        var (systemStatus, systemProblem) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/lookup-tables/{system}", admin, organization, new { name = "Despesas", isActive = false });
        var (renameStatus, renamed) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/lookup-tables/{system}", admin, organization, new { name = "Despesas", isActive = true });
        var (tableTakenStatus, _) = await _api.SendAsync(HttpMethod.Post, "/api/v1/lookup-tables", admin, organization, new { name = "Despesas" });

        second.GetProperty("position").GetInt32().ShouldBe(2);
        duplicateStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(duplicate).ShouldBe("platform.lookup_name_taken");
        itemStatus.ShouldBe(HttpStatusCode.OK);
        item.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        systemStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(systemProblem).ShouldBe("platform.lookup_table_system_locked");
        renameStatus.ShouldBe(HttpStatusCode.OK);
        renamed.GetProperty("name").GetString().ShouldBe("Despesas");
        tableTakenStatus.ShouldBe(HttpStatusCode.Conflict);
        var own = (await TablesAsync(organization, admin)).EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == table);
        own.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ShouldBe(["Site", "Indicação de cliente"]);
    }

    [Fact]
    public async Task Every_default_role_reads_the_fields_but_only_managers_change_them()
    {
        var (organization, admin) = await OfficeAsync();
        var lawyerRole = await _api.RoleIdAsync(organization, admin, DefaultRoles.Lawyer);
        var lawyer = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7());
        (await _api.SendAsync(HttpMethod.Put, $"/api/v1/organization/members/{lawyer.MemberId}/roles", admin, organization, new
        {
            roles = new[] { new { roleId = lawyerRole, scope = "own", scopeRefId = (Guid?)null } },
        })).Status.ShouldBe(HttpStatusCode.NoContent);

        (await _api.SendAsync(HttpMethod.Get, "/api/v1/custom-fields", lawyer.UserId, organization)).Status.ShouldBe(HttpStatusCode.OK);
        (await _api.SendAsync(HttpMethod.Get, "/api/v1/lookup-tables", lawyer.UserId, organization)).Status.ShouldBe(HttpStatusCode.OK);
        (await _api.SendAsync(HttpMethod.Post, "/api/v1/lookup-tables", lawyer.UserId, organization, new { name = "Minha tabela" })).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}

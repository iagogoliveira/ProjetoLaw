using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using LexFlow.Api.IntegrationTests;
using LexFlow.Identity.Domain.Sessions;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Platform.Domain.Jobs;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.SecurityTests;

/// <summary>
/// SecurityTests da seção 6.2, item 7: descobrem todos os endpoints pelo <see cref="EndpointDataSource"/> e verificam
/// autenticação, permissão e isolamento entre duas organizações. Endpoint novo entra sozinho; se tiver parâmetro de
/// rota, precisa de um caso em <see cref="CrossTenantCases"/>, ou o teste falha apontando a rota.
/// </summary>
public sealed partial class EndpointSecurityTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string[] CasesRead = ["cases.read"];
    private static readonly string[] ClientRole = ["client"];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record ApiEndpoint(string Method, string Route, string? Permission, bool Public)
    {
        public bool HasRouteParameter => Route.Contains('{', StringComparison.Ordinal);

        public bool OfficeScoped => Permission is not null && !PermissionCatalog.IsUniversal(Permission);
    }

    /// <summary>O recurso da Org A que a Org B tenta alcançar pela rota, e o corpo válido da requisição.</summary>
    private sealed record CrossTenantCase(Guid Id, object? Body = null);

    /// <summary>Recurso de outra organização (ou de outro usuário) para cada rota com parâmetro.</summary>
    private Dictionary<string, Func<SeededMember, Task<CrossTenantCase>>> CrossTenantCases => new(StringComparer.Ordinal)
    {
        ["GET /api/v1/jobs/{id:guid}"] = async owner => new CrossTenantCase(await CreateJobAsync(owner.OrganizationId)),
        ["DELETE /api/v1/me/sessions/{id:guid}"] = async owner => new CrossTenantCase(await CreateSessionAsync(owner.UserId)),
        ["PUT /api/v1/organization/members/{id:guid}/roles"] = owner =>
            Task.FromResult(new CrossTenantCase(owner.MemberId, new { roles = new[] { new { roleId = owner.RoleId, scope = "organization", scopeRefId = (Guid?)null } } })),
        ["PUT /api/v1/roles/{id:guid}/permissions"] = owner =>
            Task.FromResult(new CrossTenantCase(owner.RoleId, new { permissions = CasesRead })),
        ["PUT /api/v1/roles/{id:guid}"] = owner => Task.FromResult(new CrossTenantCase(owner.RoleId, new { name = "Tomado" })),
        ["DELETE /api/v1/roles/{id:guid}"] = owner => Task.FromResult(new CrossTenantCase(owner.RoleId)),
        ["DELETE /api/v1/organization/members/{id:guid}"] = owner => Task.FromResult(new CrossTenantCase(owner.MemberId)),
        ["POST /api/v1/organization/invitations/{id:guid}/resend"] = async owner => new CrossTenantCase(await InvitationOfAsync(owner)),
        ["DELETE /api/v1/organization/invitations/{id:guid}"] = async owner => new CrossTenantCase(await InvitationOfAsync(owner)),
        ["PUT /api/v1/organization/branches/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "branches", new { name = "Matriz" }), new { name = "Tomada" }),
        ["DELETE /api/v1/organization/branches/{id:guid}"] = async owner => new CrossTenantCase(await CreateAsOwnerAsync(owner, "branches", new { name = "Matriz" })),
        ["PUT /api/v1/organization/practice-areas/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "practice-areas", new { name = "Cível" }), new { name = "Tomada" }),
        ["DELETE /api/v1/organization/practice-areas/{id:guid}"] = async owner => new CrossTenantCase(await CreateAsOwnerAsync(owner, "practice-areas", new { name = "Cível" })),
        ["PUT /api/v1/organization/teams/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "teams", new { name = "Equipe" }), new { name = "Tomada" }),
        ["DELETE /api/v1/organization/teams/{id:guid}"] = async owner => new CrossTenantCase(await CreateAsOwnerAsync(owner, "teams", new { name = "Equipe" })),
        ["PUT /api/v1/organization/teams/{id:guid}/members"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "teams", new { name = "Equipe" }), new { memberIds = Array.Empty<Guid>() }),
        ["PUT /api/v1/organization/cost-centers/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "cost-centers", new { code = "ADM", name = "Administrativo" }), new { code = "TOM", name = "Tomado" }),
        ["DELETE /api/v1/organization/cost-centers/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "cost-centers", new { code = "ADM", name = "Administrativo" })),
        ["PUT /api/v1/organization/partners/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "partners", new { name = "Sócia", sharePercentage = 10m }), new { name = "Tomada", sharePercentage = 10m }),
        ["DELETE /api/v1/organization/partners/{id:guid}"] = async owner =>
            new CrossTenantCase(await CreateAsOwnerAsync(owner, "partners", new { name = "Sócia", sharePercentage = 10m })),
        ["PUT /api/v1/custom-fields/{id:guid}"] = async owner =>
            new CrossTenantCase(await PostAsOwnerAsync(owner, "/api/v1/custom-fields", new { entity = "cases", key = $"campo_{Guid.CreateVersion7():N}", type = "text", label = "Campo" }), new { label = "Tomado" }),
        ["PUT /api/v1/lookup-tables/{id:guid}"] = async owner =>
            new CrossTenantCase(await LookupTableOfAsync(owner), new { name = $"Tomada {Guid.CreateVersion7():N}", isActive = true }),
        ["POST /api/v1/lookup-tables/{id:guid}/items"] = async owner => new CrossTenantCase(await LookupTableOfAsync(owner), new { name = "Item" }),
        ["GET /api/v1/people/{id:guid}"] = async owner => new CrossTenantCase(await PersonOfAsync(owner)),
        ["PUT /api/v1/people/{id:guid}"] = async owner => new CrossTenantCase(await PersonOfAsync(owner), new { name = "Tomada", roles = ClientRole }),
        ["DELETE /api/v1/people/{id:guid}"] = async owner => new CrossTenantCase(await PersonOfAsync(owner)),
        ["PUT /api/v1/economic-groups/{id:guid}"] = async owner =>
            new CrossTenantCase(await PostAsOwnerAsync(owner, "/api/v1/economic-groups", new { name = $"Grupo {Guid.CreateVersion7():N}" }), new { name = "Tomado" }),
        ["DELETE /api/v1/economic-groups/{id:guid}"] = async owner =>
            new CrossTenantCase(await PostAsOwnerAsync(owner, "/api/v1/economic-groups", new { name = $"Grupo {Guid.CreateVersion7():N}" })),
        ["PUT /api/v1/lookup-items/{id:guid}"] = async owner =>
            new CrossTenantCase(await PostAsOwnerAsync(owner, $"/api/v1/lookup-tables/{await LookupTableOfAsync(owner)}/items", new { name = "Item" }), new { name = "Tomado", position = 1, isActive = true }),
    };

    private IReadOnlyList<ApiEndpoint> Endpoints() =>
        [.. _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [HttpMethods.Get]).Select(method => new ApiEndpoint(
                method,
                e.RoutePattern.RawText!,
                e.Metadata.GetMetadata<RequiredPermissionAttribute>()?.Permission,
                e.Metadata.GetMetadata<PublicEndpointAttribute>() is not null)))];

    [GeneratedRegex(@"\{[^}]+\}", RegexOptions.None, 1000)]
    private static partial Regex RouteParameter();

    private static string Fill(string route, Guid id) => RouteParameter().Replace(route, id.ToString());

    private async Task<HttpResponseMessage> SendAsync(ApiEndpoint endpoint, string path, SeededMember? caller, object? body)
    {
        using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), path);
        if (caller is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeader, caller.UserId.ToString());
            request.Headers.Add(TenantResolutionMiddleware.HeaderName, caller.OrganizationId.ToString());
        }

        if (endpoint.Method is "POST" or "PUT" or "PATCH")
        {
            request.Content = JsonContent.Create(body ?? new { });
        }

        return await _client.SendAsync(request, Ct);
    }

    /// <summary>Membro com todas as permissões atribuíveis numa organização nova.</summary>
    private Task<SeededMember> FullAccessMemberAsync() =>
        TestOrganizations.CreateWithMemberAsync(dependencies, [.. PermissionCatalog.Assignable.Select(p => p.Name)]);

    /// <summary>
    /// Cadastro de estrutura da organização do dono, pela própria API. O nome leva um sufixo único porque a mesma
    /// organização recebe vários casos.
    /// </summary>
    private async Task<Guid> CreateAsOwnerAsync(SeededMember owner, string resource, object body)
    {
        var values = System.Text.Json.JsonSerializer.SerializeToNode(body)!.AsObject();
        values["name"] = $"{values["name"]} {Guid.CreateVersion7():N}";
        if (values.ContainsKey("code"))
        {
            values["code"] = $"C{Guid.CreateVersion7():N}"[..20];
        }

        return await PostAsOwnerAsync(owner, $"/api/v1/organization/{resource}", values);
    }

    private sealed record CreatedResource(Guid Id);

    /// <summary>Recurso da organização do dono criado pela própria API (resposta 201 com <c>id</c>).</summary>
    private async Task<Guid> PostAsOwnerAsync(SeededMember owner, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add(TestAuthenticationHandler.UserHeader, owner.UserId.ToString());
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, owner.OrganizationId.ToString());
        using var response = await _client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<CreatedResource>(Ct))!.Id;
    }

    private Task<Guid> PersonOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/people", new { kind = "company", name = $"Pessoa {Guid.CreateVersion7():N}", roles = ClientRole });

    private Task<Guid> LookupTableOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/lookup-tables", new { name = $"Tabela {Guid.CreateVersion7():N}" });

    /// <summary>Convite pendente da organização do dono, para o perfil dele.</summary>
    private Task<Guid> InvitationOfAsync(SeededMember owner) =>
        PostAsOwnerAsync(owner, "/api/v1/organization/invitations", new
        {
            email = $"{Guid.CreateVersion7():N}@exemplo.com.br",
            roles = new[] { new { roleId = owner.RoleId, scope = "own" } },
        });

    private async Task<Guid> CreateJobAsync(Guid organization)
    {
        var id = Guid.CreateVersion7();
        using var ambient = AmbientContext.Begin(organization, "security-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Jobs.Add(Job.Enqueue(id, "documents.import"));
        await db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Guid> CreateSessionAsync(Guid userId)
    {
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        db.Users.Add(ApplicationUser.Register(userId, $"{userId:N}@exemplo.com.br", "Dono da sessão", new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero)));
        var session = Session.Start(Guid.CreateVersion7(), userId, "203.0.113.9", "SecurityTests", new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        db.Sessions.Add(session);
        await db.SaveChangesAsync(Ct);
        return session.Id;
    }

    [Fact]
    public void Discovery_finds_the_api_endpoints()
    {
        var endpoints = Endpoints();

        endpoints.Count.ShouldBeGreaterThan(10);
        endpoints.ShouldContain(e => e.Route == "/api/v1/jobs/{id:guid}");
    }

    [Fact]
    public async Task Every_protected_endpoint_rejects_anonymous_requests()
    {
        var failures = new List<string>();
        foreach (var endpoint in Endpoints().Where(e => !e.Public))
        {
            using var response = await SendAsync(endpoint, Fill(endpoint.Route, Guid.CreateVersion7()), caller: null, body: null);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                failures.Add($"{endpoint.Method} {endpoint.Route} → {(int)response.StatusCode}");
            }
        }

        failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_office_endpoint_refuses_a_member_without_the_permission()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies);
        var failures = new List<string>();
        foreach (var endpoint in Endpoints().Where(e => e.OfficeScoped))
        {
            using var response = await SendAsync(endpoint, Fill(endpoint.Route, Guid.CreateVersion7()), member, body: null);
            if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{endpoint.Method} {endpoint.Route} → {(int)response.StatusCode}");
            }
        }

        failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Every_route_with_an_id_has_a_cross_tenant_case()
    {
        var cases = CrossTenantCases;
        var missing = Endpoints()
            .Where(e => !e.Public && e.HasRouteParameter)
            .Select(e => $"{e.Method} {e.Route}")
            .Where(key => !cases.ContainsKey(key))
            .ToList();

        missing.ShouldBeEmpty("Endpoint com parâmetro de rota sem caso entre organizações nos SecurityTests.");
    }

    /// <summary>Critério de aceite da Etapa 3: o usuário da Org A recebe 404 em qualquer recurso da Org B.</summary>
    [Fact]
    public async Task Resources_of_another_organization_or_user_answer_404()
    {
        var owner = await FullAccessMemberAsync();
        var intruder = await FullAccessMemberAsync();
        var cases = CrossTenantCases;
        var failures = new List<string>();

        foreach (var endpoint in Endpoints().Where(e => !e.Public && e.HasRouteParameter))
        {
            var target = await cases[$"{endpoint.Method} {endpoint.Route}"](owner);
            using var response = await SendAsync(endpoint, Fill(endpoint.Route, target.Id), intruder, target.Body);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                failures.Add($"{endpoint.Method} {endpoint.Route} → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(Ct)}");
            }
        }

        failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Lists_never_include_data_of_another_organization()
    {
        var owner = await FullAccessMemberAsync();
        var reader = await FullAccessMemberAsync();
        var secrets = new[] { owner.OrganizationId, owner.MemberId, owner.RoleId, owner.UserId }.Select(id => id.ToString()).ToList();
        var failures = new List<string>();

        foreach (var endpoint in Endpoints().Where(e => !e.Public && !e.HasRouteParameter && e.Method == HttpMethods.Get))
        {
            using var response = await SendAsync(endpoint, endpoint.Route, reader, body: null);
            var content = await response.Content.ReadAsStringAsync(Ct);
            if (secrets.Exists(secret => content.Contains(secret, StringComparison.OrdinalIgnoreCase)))
            {
                failures.Add($"GET {endpoint.Route} expõe dados de outra organização");
            }
        }

        failures.ShouldBeEmpty();
    }
}

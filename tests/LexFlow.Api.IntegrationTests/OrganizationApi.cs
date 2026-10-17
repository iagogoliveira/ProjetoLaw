using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Web.Common.Security;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Chamadas à API como um usuário (esquema de teste) numa organização, com o corpo da resposta já lido.</summary>
internal sealed class OrganizationApi(HttpClient client)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary><paramref name="user"/> nulo = requisição anônima.</summary>
    public async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(HttpMethod method, string path, Guid? user, Guid? organization = null, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (user is { } id)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeader, id.ToString());
        }

        if (organization is { } org)
        {
            request.Headers.Add(TenantResolutionMiddleware.HeaderName, org.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    /// <summary>Escritório criado pelo onboarding real: o usuário devolvido é o Administrador do escritório.</summary>
    public async Task<(Guid Organization, Guid Admin)> OnboardAsync(string name = "Escritório")
    {
        var admin = Guid.CreateVersion7();
        var (status, body) = await SendAsync(HttpMethod.Post, "/api/v1/organizations", admin, body: new { name });
        status.ShouldBe(HttpStatusCode.Created);
        return (body.GetProperty("id").GetGuid(), admin);
    }

    public async Task<Guid> RoleIdAsync(Guid organization, Guid admin, string systemKey) =>
        (await SendAsync(HttpMethod.Get, "/api/v1/roles", admin, organization)).Body.EnumerateArray()
            .Single(r => r.GetProperty("systemKey").ValueKind == JsonValueKind.String && r.GetProperty("systemKey").GetString() == systemKey)
            .GetProperty("id").GetGuid();

    /// <summary>Cria pela API e devolve o id do recurso (resposta 201 com o corpo).</summary>
    public async Task<Guid> CreateAsync(string path, Guid user, Guid organization, object body)
    {
        var (status, created) = await SendAsync(HttpMethod.Post, path, user, organization, body);
        status.ShouldBe(HttpStatusCode.Created, created.ToString());
        return created.GetProperty("id").GetGuid();
    }

    public static string? Code(JsonElement problem) => problem.GetProperty("code").GetString();
}

/// <summary>Contas de usuário direto no banco (módulo Identity), para testes que precisam de nome e e-mail reais.</summary>
internal static class TestAccounts
{
    public static string NewEmail() => $"{Guid.CreateVersion7():N}@exemplo.com.br";

    public static async Task<ApplicationUser> CreateAsync(DependenciesFixture dependencies, string email, bool emailConfirmed = true, string fullName = "Pessoa Convidada")
    {
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var user = ApplicationUser.Register(Guid.CreateVersion7(), email, fullName, new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        user.EmailConfirmed = emailConfirmed;
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }
}

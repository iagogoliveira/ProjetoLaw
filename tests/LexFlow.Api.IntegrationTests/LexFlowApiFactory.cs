using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace LexFlow.Api.IntegrationTests;

/// <summary>API em memória configurada com as dependências do <see cref="DependenciesFixture"/>.</summary>
/// <param name="isolateMessaging">
/// Desliga transportes e persistência do Wolverine (que conectam ao subir) nos testes que derrubam dependências de propósito.
/// </param>
public sealed class LexFlowApiFactory(IReadOnlyDictionary<string, string> settings, bool isolateMessaging = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting chega ao Program.cs antes de ele ler a configuração (minimal hosting).
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        // Autentica quando a requisição traz X-Test-User (sem ele, segue anônima → 401 nos endpoints protegidos).
        builder.ConfigureTestServices(services => services
            .AddAuthentication(TestAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, null));

        if (isolateMessaging)
        {
            builder.ConfigureTestServices(services =>
            {
                services.DisableAllExternalWolverineTransports();
                services.DisableAllWolverineMessagePersistence();
            });
        }
    }
}

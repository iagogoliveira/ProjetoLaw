using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

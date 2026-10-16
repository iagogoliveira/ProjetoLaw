using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexFlow.Api.IntegrationTests;

/// <summary>API em memória configurada com as dependências do <see cref="DependenciesFixture"/>.</summary>
public sealed class LexFlowApiFactory(IReadOnlyDictionary<string, string> settings) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting chega ao Program.cs antes de ele ler a configuração (minimal hosting).
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}

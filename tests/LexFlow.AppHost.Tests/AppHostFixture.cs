using Aspire.Hosting;
using Aspire.Hosting.Testing;

[assembly: AssemblyFixture(typeof(LexFlow.AppHost.Tests.AppHostFixture))]

namespace LexFlow.AppHost.Tests;

/// <summary>Sobe o AppHost inteiro uma vez por assembly, sem volumes de dados.</summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    /// <summary>Primeira subida baixa imagens e instala pacotes npm; ClamAV e Gotenberg são grandes.</summary>
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(10);

    private DistributedApplication? _app;

    public DistributedApplication App => _app ?? throw new InvalidOperationException("AppHost não iniciado.");

    public async ValueTask InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.LexFlow_AppHost>(
            ["LexFlow:UseDataVolumes=false"],
            (options, _) => options.DisableDashboard = true);

        _app = await builder.BuildAsync();

        using var cts = new CancellationTokenSource(StartupTimeout);
        await _app.StartAsync(cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}

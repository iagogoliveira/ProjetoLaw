using Aspire.Hosting;
using Aspire.Hosting.Testing;

namespace LexFlow.AppHost.Tests;

/// <summary>Sobe o AppHost inteiro uma única vez (containers reais; requer Docker).</summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(15);

    public DistributedApplication App { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var cts = new CancellationTokenSource(StartupTimeout);
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.LexFlow_AppHost>(cts.Token);
        App = await builder.BuildAsync(cts.Token);
        await App.StartAsync(cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }
}

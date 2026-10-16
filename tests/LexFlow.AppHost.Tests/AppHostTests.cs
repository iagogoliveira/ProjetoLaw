using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace LexFlow.AppHost.Tests;

/// <summary>
/// Critério de aceite da Etapa 0: o AppHost sobe API, Worker, Web e todos os containers saudáveis.
/// </summary>
public sealed class AppHostTests(AppHostFixture fixture)
{
    public static TheoryData<string> HealthCheckedResources =>
        ["postgres", "lexflow", "redis", "rabbit", "storage", "gotenberg", "mailpit", "api", "web"];

    public static TheoryData<string> RunningResources => ["clamav", "worker"];

    [Theory]
    [MemberData(nameof(HealthCheckedResources))]
    public async Task Resource_becomes_healthy(string resource)
    {
        using var cts = new CancellationTokenSource(AppHostFixture.StartupTimeout);

        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync(resource, cts.Token);
    }

    /// <summary>Recursos sem endpoint HTTP de saúde: basta estarem em execução (e não terem caído).</summary>
    [Theory]
    [MemberData(nameof(RunningResources))]
    public async Task Resource_is_running(string resource)
    {
        using var cts = new CancellationTokenSource(AppHostFixture.StartupTimeout);

        var resourceEvent = await fixture.App.ResourceNotifications.WaitForResourceAsync(
            resource,
            e => e.Snapshot.State?.Text is "Running" or "Exited" or "FailedToStart" or "Finished",
            cts.Token);

        resourceEvent.Snapshot.State?.Text.ShouldBe(KnownResourceStates.Running);
    }

    [Fact]
    public async Task Api_ready_endpoint_returns_200()
    {
        using var cts = new CancellationTokenSource(AppHostFixture.StartupTimeout);
        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);

        using var client = fixture.App.CreateHttpClient("api");
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), cts.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Web_serves_the_angular_app_in_pt_BR()
    {
        using var cts = new CancellationTokenSource(AppHostFixture.StartupTimeout);
        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync("web", cts.Token);

        using var client = fixture.App.CreateHttpClient("web");
        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), cts.Token);

        html.ShouldContain("<html lang=\"pt-BR\">");
        html.ShouldContain("<lf-root>");
    }
}

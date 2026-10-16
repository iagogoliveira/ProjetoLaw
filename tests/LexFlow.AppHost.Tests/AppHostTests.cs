using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.AppHost.Tests;

[Trait("Category", "RequiresDocker")]
public sealed class AppHostTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    [Fact]
    public async Task AllResources_BecomeHealthy()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(AppHostFixture.StartupTimeout);
        var model = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var names = model.Resources
            .Where(r => r is not ParameterResource)
            .Select(r => r.Name)
            .ToList();

        names.ShouldContain("api", StringComparer.Ordinal);
        names.ShouldContain("worker", StringComparer.Ordinal);
        names.ShouldContain("web", StringComparer.Ordinal);

        foreach (var name in names)
        {
            await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync(name, cts.Token);
        }
    }

    [Fact]
    public async Task ApiHealthReady_Returns200_WithSqlRedisRabbitBlob()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(AppHostFixture.StartupTimeout);
        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync("api", cts.Token);
        using var client = fixture.App.CreateHttpClient("api");

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), cts.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<HealthReportDto>(cts.Token);
        report.ShouldNotBeNull();
        report.Status.ShouldBe("Healthy");
        var names = report.Checks.Select(c => c.Name).ToList();
        names.ShouldContain(n => n.Contains("Sql", StringComparison.OrdinalIgnoreCase));
        names.ShouldContain(n => n.Contains("Redis", StringComparison.OrdinalIgnoreCase));
        names.ShouldContain(n => n.Contains("Rabbit", StringComparison.OrdinalIgnoreCase));
        names.ShouldContain(n => n.Contains("Blob", StringComparison.OrdinalIgnoreCase));
        report.Checks.ShouldAllBe(c => c.Status == "Healthy");
    }

    private sealed record HealthReportDto(string Status, IReadOnlyList<HealthCheckDto> Checks);

    private sealed record HealthCheckDto(string Name, string Status);
}

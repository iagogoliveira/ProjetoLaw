using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexFlow.Api.IntegrationTests;

public sealed class HealthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Live_Returns200()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_ListsSqlRedisRabbitBlobChecks()
    {
        factory.DependencyStatus = HealthStatus.Healthy;
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<HealthReportDto>(TestContext.Current.CancellationToken);
        report.ShouldNotBeNull();
        report.Status.ShouldBe("Healthy");
        var names = report.Checks.Select(c => c.Name).ToList();
        names.ShouldContain(n => n.Contains("Sql", StringComparison.OrdinalIgnoreCase));
        names.ShouldContain(n => n.Contains("Redis", StringComparison.OrdinalIgnoreCase));
        names.ShouldContain(n => n.Contains("Rabbit", StringComparison.OrdinalIgnoreCase));
        names.ShouldContain(n => n.Contains("Blob", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ready_Returns503_WhenDependencyIsUnhealthy()
    {
        factory.DependencyStatus = HealthStatus.Unhealthy;
        try
        {
            using var client = factory.CreateClient();

            var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
            var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
            live.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            factory.DependencyStatus = HealthStatus.Healthy;
        }
    }

    private sealed record HealthReportDto(string Status, IReadOnlyList<HealthCheckDto> Checks);

    private sealed record HealthCheckDto(string Name, string Status);
}

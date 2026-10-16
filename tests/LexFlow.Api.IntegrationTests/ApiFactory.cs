using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Sobe a API em memória sem dependências reais. Os health checks das dependências
/// (SQL, Redis, RabbitMQ, Blob) mantêm o nome original, mas respondem com um resultado fixo.
/// A verificação contra containers reais está em <c>LexFlow.AppHost.Tests</c>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public HealthStatus DependencyStatus { get; set; } = HealthStatus.Healthy;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:lexflow", "Server=localhost,1;Database=lexflow;User Id=sa;Password=fake;TrustServerCertificate=true");
        builder.UseSetting("ConnectionStrings:redis", "localhost:1,abortConnect=false");
        builder.UseSetting("ConnectionStrings:rabbit", "amqp://guest:guest@localhost:1");
        builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");

        builder.ConfigureServices(services =>
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                foreach (var registration in options.Registrations.Where(r => !r.Tags.Contains("live")))
                {
                    registration.Factory = _ => new FixedHealthCheck(() => DependencyStatus);
                }
            }));
    }

    private sealed class FixedHealthCheck(Func<HealthStatus> status) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HealthCheckResult(status()));
    }
}

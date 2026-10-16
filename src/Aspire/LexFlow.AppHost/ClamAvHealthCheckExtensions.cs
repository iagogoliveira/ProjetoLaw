using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexFlow.AppHost;

internal static class ClamAvHealthCheckExtensions
{
    public static IResourceBuilder<ContainerResource> WithClamAvHealthCheck(this IResourceBuilder<ContainerResource> builder)
    {
        var key = $"{builder.Resource.Name}_clamd";
        var endpoint = builder.GetEndpoint("tcp");
        builder.ApplicationBuilder.Services.AddHealthChecks()
            .Add(new HealthCheckRegistration(key, _ => new ClamAvHealthCheck(endpoint), failureStatus: null, tags: null));
        return builder.WithHealthCheck(key);
    }
}

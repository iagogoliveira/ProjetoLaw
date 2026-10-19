using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Contracts;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Publications.Application;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Infrastructure.Capture;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Publications.Infrastructure;

public static class PublicationsInfrastructure
{
    public static IServiceCollection AddPublicationsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PublicationsDbContext>(configuration, PublicationsDbContext.ModuleName, PublicationsDbContext.SchemaName);
        services.AddScoped<IPublicationsDbContext>(sp => sp.GetRequiredService<PublicationsDbContext>());
        return services;
    }

    public static IServiceCollection AddPublicationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPublicationsPersistence(configuration);
        services.AddScoped<PublicationAccess>();
        services.AddScoped<ICaptureLocks, PostgresCaptureLocks>();
        services.AddScoped<CaptureSourceJob>();
        services.AddScoped<CaptureMonitor>();
        services.AddScoped<CaptureQueriesAccess>();
        services.AddScoped<CaptureDispatchJobs>();
        services.AddScoped<ICaptureAlertDirectory, CaptureAlertDirectory>();
        services.AddScoped<INotificationVisibility, PublicationNotificationVisibility>();
        AddProviders(services, configuration);
        services.AddScoped<ITimelineContributor, PublicationsTimeline>();
        return services;
    }
    public static IServiceCollection AddPublicationsRecurringJobs(this IServiceCollection services) =>
        services.AddHostedService<CaptureRecurringJobs>();

    private static void AddProviders(IServiceCollection services, IConfiguration configuration)
    {
        var name = configuration["Publications:Provider"] ?? "Disabled";
        if (string.Equals(name, "Fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(configuration.GetSection("Publications:Fake").Get<FakeCaptureOptions>() ?? new FakeCaptureOptions());
            services.AddSingleton<FakePublicationProvider>();
            services.AddSingleton<IPublicationProvider>(sp => sp.GetRequiredService<FakePublicationProvider>());
            services.AddSingleton<IMovementProvider>(sp => sp.GetRequiredService<FakePublicationProvider>());
        }
        else if (string.Equals(name, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<DisabledPublicationProvider>();
            services.AddSingleton<IPublicationProvider>(sp => sp.GetRequiredService<DisabledPublicationProvider>());
            services.AddSingleton<IMovementProvider>(sp => sp.GetRequiredService<DisabledPublicationProvider>());
        }
        else { throw new InvalidOperationException("Provedor Publications desconhecido."); }
    }
}

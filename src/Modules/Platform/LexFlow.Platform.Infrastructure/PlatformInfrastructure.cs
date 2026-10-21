using LexFlow.Application.Abstractions.Idempotency;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Application.Calendar;
using LexFlow.Platform.Application.Courts;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Application.Indexes;
using LexFlow.Platform.Application.Jobs;
using LexFlow.Platform.Application.Notifications;
using LexFlow.Platform.Contracts.Calendar;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Contracts.Indexes;
using LexFlow.Platform.Contracts.Jobs;
using LexFlow.Platform.Infrastructure.Idempotency;
using LexFlow.Platform.Infrastructure.Indexes;
using LexFlow.Platform.Infrastructure.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LexFlow.Platform.Infrastructure;

public static class PlatformInfrastructure
{
    public static IServiceCollection AddPlatformNotifications(this IServiceCollection services)
    {
        services.AddScoped<NotificationAccess>();
        services.AddScoped<NotificationDeliveryPolicy>();
        services.AddScoped<NotificationSettingsAccess>();
        services.AddScoped<INotificationReadLock, NotificationReadLock>();
        return services;
    }

    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PlatformDbContext>(configuration, PlatformDbContext.ModuleName, PlatformDbContext.SchemaName);
        services.AddScoped<IPlatformDbContext>(sp => sp.GetRequiredService<PlatformDbContext>());
        services.AddSingleton<IIdempotencyStore, PlatformIdempotencyStore>();
        services.AddScoped<IdempotencyKeysCleanupJob>();
        services.AddScoped<ICustomFieldValidator, CustomFieldValidator>();
        services.AddScoped<ILookupItemValidator, LookupItemValidator>();
        services.AddScoped<ILookupItems, LookupItems>();
        services.AddScoped<IJobTracker, JobTracker>();
        services.AddScoped<ICourtDirectory, CourtDirectory>();
        services.AddScoped<IHolidayDirectory, HolidayDirectory>();
        services.AddScoped<LookupTableSeeder>();
        services.AddScoped<IMonetaryCorrection, MonetaryCorrection>();
        services.AddScoped<MonetaryIndexImporter>();
        services.AddScoped<MonetaryIndexImportJob>();
        services.AddScoped<DailySchedulingDispatchJob>();
        services.AddScoped<DailyOfficeNotificationsJob>();
        services.AddScoped<DailyDigestJob>();
        AddIndexSources(services, configuration);
        return services;
    }

    /// <summary>Fontes dos índices, na ordem em que o importador tenta: IBGE; BCB e, se ele falhar, o Ipeadata.</summary>
    private static void AddIndexSources(IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(MonetaryIndexSettings.SectionName).Get<MonetaryIndexSettings>() ?? new MonetaryIndexSettings();
        if (string.Equals(settings.Source, "Fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IMonetaryIndexSource, FakeIndexSource>();
            return;
        }

        services.AddHttpClient<IbgeSidraIndexSource>(client => Configure(client, settings.IbgeBaseAddress));
        services.AddHttpClient<BcbSgsIndexSource>(client => Configure(client, settings.BcbBaseAddress));
        services.AddHttpClient<IpeadataIndexSource>(client => Configure(client, settings.IpeadataBaseAddress));
        services.AddTransient<IMonetaryIndexSource>(sp => sp.GetRequiredService<IbgeSidraIndexSource>());
        services.AddTransient<IMonetaryIndexSource>(sp => sp.GetRequiredService<BcbSgsIndexSource>());
        services.AddTransient<IMonetaryIndexSource>(sp => sp.GetRequiredService<IpeadataIndexSource>());

        static void Configure(HttpClient client, Uri baseAddress)
        {
            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("LexFlow");
        }
    }

    /// <summary>Jobs recorrentes do módulo (registrados pelo Worker na inicialização).</summary>
    public static IServiceCollection AddPlatformRecurringJobs(this IServiceCollection services) =>
        services.AddHostedService<PlatformRecurringJobsRegistration>();

    private sealed class PlatformRecurringJobsRegistration(IServiceScopeFactory scopes) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IJobScheduler>().AddOrUpdateRecurring<IdempotencyKeysCleanupJob>(
                IdempotencyKeysCleanupJob.RecurringJobId,
                job => job.RunAsync(),
                IdempotencyKeysCleanupJob.Cron);
            scope.ServiceProvider.GetRequiredService<IJobScheduler>().AddOrUpdateRecurring<MonetaryIndexImportJob>(
                MonetaryIndexImportJob.RecurringJobId,
                job => job.RunAsync(),
                MonetaryIndexImportJob.Cron);
            scope.ServiceProvider.GetRequiredService<IJobScheduler>().AddOrUpdateRecurring<DailySchedulingDispatchJob>(
                DailySchedulingDispatchJob.RecurringJobId,
                job => job.RunAsync(),
                DailySchedulingDispatchJob.Cron);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

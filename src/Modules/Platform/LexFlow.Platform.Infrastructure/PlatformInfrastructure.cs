using LexFlow.Application.Abstractions.Idempotency;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.Platform.Infrastructure.Idempotency;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LexFlow.Platform.Infrastructure;

public static class PlatformInfrastructure
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PlatformDbContext>(configuration, PlatformDbContext.ModuleName, PlatformDbContext.SchemaName);
        services.AddScoped<IPlatformDbContext>(sp => sp.GetRequiredService<PlatformDbContext>());
        services.AddSingleton<IIdempotencyStore, PlatformIdempotencyStore>();
        services.AddScoped<IdempotencyKeysCleanupJob>();
        services.AddScoped<ICustomFieldValidator, CustomFieldValidator>();
        services.AddScoped<LookupTableSeeder>();
        return services;
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
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

using System.Diagnostics;
using LexFlow.Infrastructure.Common.Integrations;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.MigrationService;

/// <summary>DbContexts cujas migrations serão aplicadas, na ordem.</summary>
public sealed record MigrationPlan(IReadOnlyList<Type> DbContextTypes);

internal sealed partial class MigrationRunner(
    IServiceProvider services,
    MigrationPlan plan,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationRunner> logger) : BackgroundService
{
    public static readonly ActivitySource ActivitySource = new("LexFlow.MigrationService");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = ActivitySource.StartActivity("Aplicar migrations");
        try
        {
            foreach (var contextType in plan.DbContextTypes)
            {
                await using var scope = services.CreateAsyncScope();
                var context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
                var pending = (await context.Database.GetPendingMigrationsAsync(stoppingToken)).ToList();
                await context.Database.MigrateAsync(stoppingToken);
                LogApplied(logger, contextType.Name, pending.Count);
            }

            await services.GetRequiredService<IObjectStorageProvisioner>().EnsureBucketAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            LogFailed(logger, ex);
            Environment.ExitCode = 1;
            throw;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Context}: {Count} migration(s) aplicada(s)")]
    private static partial void LogApplied(ILogger logger, string context, int count);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Falha ao aplicar migrations")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}

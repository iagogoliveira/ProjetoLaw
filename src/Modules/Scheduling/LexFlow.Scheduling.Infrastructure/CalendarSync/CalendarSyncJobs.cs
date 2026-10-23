using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Scheduling.Application.CalendarSync;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Scheduling.Infrastructure.CalendarSync;

internal sealed class CalendarSyncScheduler(IJobScheduler jobs) : ICalendarSyncScheduler
{
    public void Enqueue(Guid organizationId, Guid userId, Guid connectionId) =>
        jobs.Enqueue<CalendarSyncJob>(job => job.RunAsync(organizationId, userId, connectionId));
}

public sealed class CalendarSyncJob(IServiceScopeFactory scopes)
{
    public async Task RunAsync(Guid organizationId, Guid userId, Guid connectionId)
    {
        using var ambient = AmbientContext.Begin(organizationId, "calendar-sync");
        await using var scope = scopes.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(organizationId);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        await using var transaction = await db.BeginTransactionAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<CalendarSyncEngine>().RunAsync(connectionId, userId, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
    }
}

public sealed class CalendarSyncDispatchJob(IServiceScopeFactory scopes)
{
    public const string RecurringJobId = "scheduling-calendar-sync";
    public const string Cron = "*/5 * * * *";

    public async Task RunAsync()
    {
        await using var root = scopes.CreateAsyncScope();
        if (!root.ServiceProvider.GetRequiredService<ICalendarSyncProvider>().IsEnabled) { return; }
        var offices = await root.ServiceProvider.GetRequiredService<IOrganizationAccessDirectory>().OfficeIdsAsync(CancellationToken.None);
        foreach (var office in offices)
        {
            await using var scope = scopes.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office);
            var connections = await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().CalendarConnections.AsNoTracking()
                .Where(c => c.IsActive).Select(c => new { c.UserId, c.Id }).ToListAsync();
            var jobs = scope.ServiceProvider.GetRequiredService<ICalendarSyncScheduler>();
            foreach (var connection in connections) { jobs.Enqueue(office, connection.UserId, connection.Id); }
        }
    }
}

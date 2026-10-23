using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Contracts;
using LexFlow.Platform.Contracts.Control;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Scheduling.Application;
using LexFlow.Scheduling.Application.Calendar;
using LexFlow.Scheduling.Application.CalendarSync;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.Scheduling.Infrastructure.CalendarSync;
using LexFlow.Scheduling.Infrastructure.Control;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LexFlow.Scheduling.Infrastructure;

public static class SchedulingInfrastructure
{
    public static IServiceCollection AddSchedulingPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<SchedulingDbContext>(configuration, SchedulingDbContext.ModuleName, SchedulingDbContext.SchemaName);
        services.AddScoped<ISchedulingDbContext>(sp => sp.GetRequiredService<SchedulingDbContext>());
        return services;
    }
    public static IServiceCollection AddSchedulingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSchedulingPersistence(configuration);
        services.AddScoped<WorkAssignees>();
        services.AddScoped<DeadlineAccess>();
        services.AddScoped<AgendaAccess>();
        services.AddScoped<AgendaFilters>();
        services.AddScoped<AgendaSources>();
        services.AddScoped<TaskWrites>();
        services.AddScoped<CalendarDeadlineAlerts>();
        services.AddScoped<ITimelineContributor, SchedulingTimeline>();
        services.AddScoped<DeadlineNotificationVisibility>();
        services.AddScoped<INotificationVisibility>(sp => sp.GetRequiredService<DeadlineNotificationVisibility>());
        services.AddScoped<IDeadlineAlertDirectory, DeadlineAlertDirectory>();
        services.AddScoped<IFatalDeadlineReminders, FatalDeadlineReminders>();
        services.AddScoped<IControlReportSource, SchedulingControlReport>();
        services.AddScoped<IDailyDigestSource, SchedulingControlReport>();
        services.AddScoped<ISchedulingIntake, SchedulingIntake>();
        services.AddSingleton<IDeadlineCalculator, DeadlineCalculator>();
        services.AddSingleton(configuration.GetSection("Scheduling:CalendarSync:Fake").Get<FakeCalendarSyncOptions>() ?? new FakeCalendarSyncOptions());
        services.AddSingleton(new CalendarSyncSettings(configuration["Scheduling:CalendarSync:PublicWebUrl"] ?? "http://localhost:4200"));
        services.AddScoped<ICalendarSyncProvider, PersistentCalendarSyncProvider>();
        services.AddScoped<CalendarSyncAccess>();
        services.AddScoped<CalendarSyncEngine>();
        services.AddScoped<ICalendarSyncScheduler, CalendarSyncScheduler>();
        services.AddScoped<CalendarSyncJob>();
        services.AddScoped<CalendarSyncDispatchJob>();
        return services;
    }

    public static IServiceCollection AddSchedulingRecurringJobs(this IServiceCollection services) => services.AddHostedService<CalendarJobsRegistration>();

    private sealed class CalendarJobsRegistration(IServiceScopeFactory scopes) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IJobScheduler>().AddOrUpdateRecurring<CalendarSyncDispatchJob>(
                CalendarSyncDispatchJob.RecurringJobId, job => job.RunAsync(), CalendarSyncDispatchJob.Cron);
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

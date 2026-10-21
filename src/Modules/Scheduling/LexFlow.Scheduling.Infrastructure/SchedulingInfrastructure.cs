using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Contracts;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Scheduling.Application;
using LexFlow.Scheduling.Application.Calendar;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<ISchedulingIntake, SchedulingIntake>();
        services.AddSingleton<IDeadlineCalculator, DeadlineCalculator>();
        return services;
    }
}

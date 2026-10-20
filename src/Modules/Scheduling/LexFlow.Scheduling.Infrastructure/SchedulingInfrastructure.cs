using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Scheduling.Application;
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
        services.AddScoped<ISchedulingIntake, SchedulingIntake>();
        services.AddSingleton<IDeadlineCalculator, DeadlineCalculator>();
        return services;
    }
}

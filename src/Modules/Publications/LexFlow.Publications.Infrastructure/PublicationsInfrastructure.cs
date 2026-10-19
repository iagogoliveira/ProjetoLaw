using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Contracts;
using LexFlow.Publications.Application;
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
        services.AddScoped<ITimelineContributor, PublicationsTimeline>();
        return services;
    }
}

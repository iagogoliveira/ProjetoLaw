using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Organization.Infrastructure;

public static class OrganizationInfrastructure
{
    public static IServiceCollection AddOrganizationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrganizationDbContext>(configuration, OrganizationDbContext.ModuleName, OrganizationDbContext.SchemaName);
        services.AddScoped<IOrganizationDbContext>(sp => sp.GetRequiredService<OrganizationDbContext>());
        return services;
    }
}

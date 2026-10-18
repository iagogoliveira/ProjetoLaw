using LexFlow.Application.Abstractions.Security;
using LexFlow.Infrastructure.Common.Caching;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Application.Access;
using LexFlow.Organization.Application.OrgStructure;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Organization.Infrastructure.Access;
using LexFlow.Organization.Infrastructure.Invitations;
using LexFlow.Organization.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexFlow.Organization.Infrastructure;

public static class OrganizationInfrastructure
{
    /// <summary>Só o DbContext do módulo (usado pelo MigrationService).</summary>
    public static IServiceCollection AddOrganizationPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<OrganizationDbContext>(configuration, OrganizationDbContext.ModuleName, OrganizationDbContext.SchemaName);
        services.AddScoped<IOrganizationDbContext>(sp => sp.GetRequiredService<OrganizationDbContext>());
        return services;
    }

    /// <summary>
    /// DbContext e autorização: pertença do usuário (valida o tenant da requisição), verificador de permissões e acesso
    /// corrente para os autorizadores de recurso, com cache no Redis. Também o e-mail dos convites (enviado pelo Worker).
    /// </summary>
    public static IServiceCollection AddOrganizationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOrganizationPersistence(configuration);
        services.AddRedisConnection(configuration);
        services.AddSingleton<IAccessCache, RedisAccessCache>();
        services.AddScoped<AccessReader>();
        services.AddScoped<AccessChanges>();
        services.AddScoped<RoleAssignments>();
        services.TryAddSingleton(configuration.GetSection(OrganizationSettings.SectionName).Get<OrganizationSettings>() ?? new OrganizationSettings());
        services.AddSingleton<InvitationEmailComposer>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<ICurrentAccess, CurrentAccess>();
        services.AddScoped<IMembershipLookup, MembershipLookup>();
        services.AddScoped<IOrganizationMembers, OrganizationMembers>();
        services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();
        return services;
    }
}

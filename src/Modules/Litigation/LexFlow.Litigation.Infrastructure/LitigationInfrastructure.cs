using LexFlow.Crm.Contracts;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Access;
using LexFlow.Litigation.Application.Cases;
using LexFlow.Litigation.Application.Claims;
using LexFlow.Litigation.Application.Hearings;
using LexFlow.Litigation.Application.Settlements;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Infrastructure.Access;
using LexFlow.Litigation.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Litigation.Infrastructure;

public static class LitigationInfrastructure
{
    /// <summary>Só o DbContext do módulo (usado pelo MigrationService).</summary>
    public static IServiceCollection AddLitigationPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<LitigationDbContext>(configuration, LitigationDbContext.ModuleName, LitigationDbContext.SchemaName);
        services.AddScoped<ILitigationDbContext>(sp => sp.GetRequiredService<LitigationDbContext>());
        return services;
    }

    /// <summary>
    /// DbContext, regras do cadastro, contador da numeração e as partes dos processos para o conflito de interesses do
    /// Crm (registradas depois do Crm, substituem o stub da Etapa 4).
    /// </summary>
    public static IServiceCollection AddLitigationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddLitigationPersistence(configuration);
        services.AddScoped<CaseRules>();
        services.AddScoped<CaseResponseBuilder>();
        services.AddScoped<ClaimResponseBuilder>();
        services.AddScoped<CaseWrites>();
        services.AddScoped<ClaimWrites>();
        services.AddScoped<SettlementActions>();
        services.AddScoped<SettlementResponses>();
        services.AddScoped<HearingRules>();
        services.AddScoped<ICaseCounter, CaseCounterStore>();
        services.AddScoped<ILitigationLookup, CasePartyLookup>();
        services.AddScoped<CaseAccess>();
        services.AddScoped<IAccessDenials, AccessDenials>();
        services.AddScoped<ICaseVisibility, CaseVisibility>();
        return services;
    }
}

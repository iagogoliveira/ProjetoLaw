using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Application.Conflicts;
using LexFlow.Crm.Application.Opportunities;
using LexFlow.Crm.Application.People;
using LexFlow.Crm.Application.Proposals;
using LexFlow.Crm.Contracts;
using LexFlow.Crm.Infrastructure.CnpjLookup;
using LexFlow.Crm.Infrastructure.Conflicts;
using LexFlow.Crm.Infrastructure.Persistence;
using LexFlow.Crm.Infrastructure.Proposals;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexFlow.Crm.Infrastructure;

public static class CrmInfrastructure
{
    /// <summary>Só o DbContext do módulo (usado pelo MigrationService).</summary>
    public static IServiceCollection AddCrmPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<CrmDbContext>(configuration, CrmDbContext.ModuleName, CrmDbContext.SchemaName);
        services.AddScoped<ICrmDbContext>(sp => sp.GetRequiredService<CrmDbContext>());
        return services;
    }

    /// <summary>
    /// DbContext, regras do cadastro e da oportunidade, os modelos DOCX de proposta e a consulta de CNPJ (BrasilAPI, ou
    /// a falsa quando configurada).
    /// </summary>
    public static IServiceCollection AddCrmInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddCrmPersistence(configuration);
        services.AddScoped<PersonRules>();
        services.AddScoped<ConflictDetector>();
        services.AddScoped<IPeopleDirectory, PeopleDirectory>();
        services.AddScoped<IPeopleRegistry, PeopleRegistry>();
        services.AddScoped<OpportunityRules>();
        services.AddScoped<ProposalDocumentComposer>();
        services.AddSingleton<IProposalDocuments, DocxProposalDocuments>();

        // Partes de processos para o conflito: o módulo de processos (Etapa 5) troca este registro.
        services.TryAddScoped<ILitigationLookup, NoCasesLitigationLookup>();

        var cnpj = configuration.GetSection(CnpjLookupSettings.SectionName).Get<CnpjLookupSettings>() ?? new CnpjLookupSettings();
        if (string.Equals(cnpj.Provider, "Fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICnpjLookup, FakeCnpjLookup>();
        }
        else
        {
            services.AddHttpClient<ICnpjLookup, BrasilApiCnpjLookup>(client =>
            {
                client.BaseAddress = cnpj.BaseAddress;
                client.Timeout = TimeSpan.FromSeconds(8);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LexFlow");
            });
        }

        return services;
    }
}

using LexFlow.Application.Abstractions.Data;
using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Infrastructure.Common.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace LexFlow.Infrastructure.Common.Persistence;

public static class PersistenceRegistration
{
    /// <summary>
    /// Registra o DbContext do módulo sobre o <see cref="NpgsqlDataSource"/> compartilhado, com snake_case, histórico
    /// de migrations no schema do módulo e os interceptors na ordem: eventos de domínio → tenant → colunas de
    /// auditoria/exclusão lógica → trilha de auditoria → outbox (se houver mensageria). Também expõe o contexto como <see cref="IUnitOfWork"/> do módulo.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string moduleName,
        string schema)
        where TContext : ModuleDbContextBase
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(_ =>
        {
            var connectionString = configuration.GetConnectionString(DependencyHealthChecks.DatabaseConnectionName)
                ?? throw new InvalidOperationException($"Connection string '{DependencyHealthChecks.DatabaseConnectionName}' não configurada.");
            return new NpgsqlDataSourceBuilder(connectionString).Build();
        });

        services.TryAddScoped<DomainEventsInterceptor>();
        services.TryAddScoped<TenantSaveChangesInterceptor>();
        services.TryAddScoped<AuditableEntitiesInterceptor>();
        services.TryAddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<TContext>((sp, options) =>
        {
            options
                .UseNpgsql(
                    sp.GetRequiredService<NpgsqlDataSource>(),
                    npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    sp.GetRequiredService<DomainEventsInterceptor>(),
                    sp.GetRequiredService<TenantSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditableEntitiesInterceptor>(),
                    sp.GetRequiredService<AuditSaveChangesInterceptor>());

            // Só quando a mensageria está configurada (API e Worker); o MigrationService não publica eventos.
            if (sp.GetService<OutboxInterceptor>() is { } outbox)
            {
                options.AddInterceptors(outbox);
            }
        });

        services.AddKeyedScoped<IUnitOfWork>(moduleName, (sp, _) => sp.GetRequiredService<TContext>());
        return services;
    }
}

using System.Reflection;
using JasperFx;
using JasperFx.CodeGeneration.Model;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace LexFlow.Infrastructure.Common.Messaging;

public static class MessagingRegistration
{
    /// <summary>
    /// Wolverine só como mensageria (ADR 0009): outbox/inbox duráveis no PostgreSQL (schema <c>wolverine</c>),
    /// transporte RabbitMQ com roteamento por tipo de mensagem e descoberta restrita aos
    /// <see cref="IIntegrationEventHandler{TEvent}"/> dos assemblies informados (a API passa nenhum: só publica).
    /// </summary>
    public static IServiceCollection AddLexFlowMessaging(this IServiceCollection services, IConfiguration configuration, params Assembly[] consumerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(consumerAssemblies);

        var database = configuration.GetConnectionString(DependencyHealthChecks.DatabaseConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{DependencyHealthChecks.DatabaseConnectionName}' não configurada.");
        var rabbit = configuration.GetConnectionString(DependencyHealthChecks.RabbitMqConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{DependencyHealthChecks.RabbitMqConnectionName}' não configurada.");

        services.AddScoped<PendingIntegrationEvents>();
        services.AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();
        services.AddScoped<OutboxInterceptor>();

        services.AddWolverine(options =>
        {
            options.ServiceName = "lexflow";
            options.PersistMessagesWithPostgresql(database, ModuleDbContextBase.WolverineSchema);
            options.AutoBuildMessageStorageOnStartup = AutoCreate.CreateOrUpdate;
            options.UseEntityFrameworkCoreTransactions();

            options.UseRabbitMq(new Uri(rabbit)).AutoProvision().UseConventionalRouting();
            options.Policies.UseDurableInboxOnAllListeners();
            options.Policies.UseDurableOutboxOnAllSendingEndpoints();

            // Consumidores recebem o DbContext do módulo, registrado por fábrica (AddDbContext): o código gerado pelo
            // Wolverine resolve essas dependências no container de escopo da mensagem.
            options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;

            // Nossos comandos/consultas também se chamam *Handler: a descoberta por convenção do Wolverine fica
            // desligada e os consumidores de eventos de integração são registrados explicitamente (mais previsível
            // que a convenção do Wolverine para interfaces genéricas abertas).
            options.Discovery.DisableConventionalDiscovery();
            foreach (var consumer in FindConsumers(consumerAssemblies))
            {
                options.Discovery.IncludeType(consumer);
            }
        });

        return services;
    }

    /// <summary>Tipos concretos que implementam <see cref="IIntegrationEventHandler{TEvent}"/>.</summary>
    internal static IReadOnlyList<Type> FindConsumers(IEnumerable<Assembly> assemblies) =>
        [.. assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>)))];
}

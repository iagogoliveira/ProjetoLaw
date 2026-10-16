using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Infrastructure.Common.HealthChecks;

/// <summary>Health checks das dependências externas, expostos em <c>/health/ready</c>.</summary>
public static class DependencyHealthChecks
{
    /// <summary>Tag dos checks que compõem a prontidão (<c>/health/ready</c>).</summary>
    public const string ReadyTag = "ready";

    /// <summary>Nome da connection string do banco principal (recurso <c>lexflow</c> do AppHost).</summary>
    public const string DatabaseConnectionName = "lexflow";

    public const string RedisConnectionName = "redis";

    public const string RabbitMqConnectionName = "rabbit";

    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

    public static IHealthChecksBuilder AddDependencyHealthChecks(this IHealthChecksBuilder builder, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        string[] tags = [ReadyTag];

        builder.AddNpgSql(RequiredConnectionString(configuration, DatabaseConnectionName), name: "postgres", tags: tags, timeout: CheckTimeout);
        builder.AddRedis(RequiredConnectionString(configuration, RedisConnectionName), name: "redis", tags: tags, timeout: CheckTimeout);

        var rabbitUri = new Uri(RequiredConnectionString(configuration, RabbitMqConnectionName));
        builder.AddTypeActivatedCheck<RabbitMqHealthCheck>("rabbitmq", failureStatus: null, tags: tags, timeout: CheckTimeout, rabbitUri);

        var storage = configuration.GetSection(ObjectStorageSettings.SectionName).Get<ObjectStorageSettings>()
            ?? throw new InvalidOperationException($"Seção de configuração '{ObjectStorageSettings.SectionName}' ausente.");
        builder.AddTypeActivatedCheck<ObjectStorageHealthCheck>("object-storage", failureStatus: null, tags: tags, timeout: CheckTimeout, storage);

        return builder;
    }

    private static string RequiredConnectionString(IConfiguration configuration, string name) =>
        configuration.GetConnectionString(name)
        ?? throw new InvalidOperationException($"Connection string '{name}' não configurada.");
}

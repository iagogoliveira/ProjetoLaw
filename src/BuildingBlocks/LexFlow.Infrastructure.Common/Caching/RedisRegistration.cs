using LexFlow.Infrastructure.Common.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace LexFlow.Infrastructure.Common.Caching;

public static class RedisRegistration
{
    /// <summary>
    /// Conexão única com o Redis (connection string <c>redis</c>), compartilhada pelos caches dos módulos. Não falha
    /// na partida se o Redis estiver fora: as operações é que falham, e cada cache decide como degradar.
    /// </summary>
    public static IServiceCollection AddRedisConnection(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = configuration.GetConnectionString(DependencyHealthChecks.RedisConnectionName)
                ?? throw new InvalidOperationException($"Connection string '{DependencyHealthChecks.RedisConnectionName}' não configurada.");
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        return services;
    }
}

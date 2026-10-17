using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Application.Access;
using LexFlow.Organization.Contracts;
using LexFlow.Organization.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LexFlow.Organization.Infrastructure.Access;

/// <summary>
/// Cache no Redis (seção 7.1: 5 min). As permissões ficam sob uma versão por organização: invalidar é incrementar a
/// versão, o que descarta de uma vez as entradas de todos os membros. Se o Redis estiver fora, lê do banco (mais
/// lento, nunca menos correto).
/// </summary>
internal sealed partial class RedisAccessCache(IConnectionMultiplexer redis, ILogger<RedisAccessCache> logger) : IAccessCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static RedisKey VersionKey(Guid organizationId) => $"lexflow:access:version:{organizationId:N}";

    private static RedisKey MembershipsKey(Guid userId) => $"lexflow:memberships:{userId:N}";

    public async Task<AccessSnapshot?> GetOrLoadAsync(Guid organizationId, Guid userId, Func<Task<AccessSnapshot?>> load, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);
        RedisKey entryKey;
        try
        {
            var version = (long?)await redis.GetDatabase().StringGetAsync(VersionKey(organizationId)) ?? 0;
            entryKey = $"lexflow:access:{organizationId:N}:{userId:N}:{version}";
            var cached = await redis.GetDatabase().StringGetAsync(entryKey);
            if (cached.HasValue)
            {
                return JsonSerializer.Deserialize<AccessSnapshot?>(cached.ToString(), Json);
            }
        }
        catch (RedisException ex)
        {
            LogUnavailable(logger, ex);
            return await load();
        }

        var snapshot = await load();
        await TryAsync(db => db.StringSetAsync(entryKey, JsonSerializer.Serialize(snapshot, Json), Lifetime));
        return snapshot;
    }

    public Task InvalidateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        TryAsync(db => db.StringIncrementAsync(VersionKey(organizationId)));

    public async Task<MembershipSnapshot?> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(MembershipsKey(userId));
            return cached.HasValue ? JsonSerializer.Deserialize<MembershipSnapshot>(cached.ToString(), Json) : null;
        }
        catch (RedisException ex)
        {
            LogUnavailable(logger, ex);
            return null;
        }
    }

    public Task SetMembershipsAsync(Guid userId, MembershipSnapshot memberships, CancellationToken cancellationToken) =>
        TryAsync(db => db.StringSetAsync(MembershipsKey(userId), JsonSerializer.Serialize(memberships, Json), Lifetime));

    public Task InvalidateMembershipsAsync(Guid userId, CancellationToken cancellationToken) =>
        TryAsync(db => db.KeyDeleteAsync(MembershipsKey(userId)));

    private async Task TryAsync(Func<IDatabase, Task> action)
    {
        try
        {
            await action(redis.GetDatabase());
        }
        catch (RedisException ex)
        {
            LogUnavailable(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache de permissões indisponível; usando o banco")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}

/// <summary>Invalida de novo, depois do commit, as permissões da organização do evento (no Worker).</summary>
public sealed class RolePermissionsChangedConsumer(OrganizationDbContext db, IClock clock, IAccessCache cache)
    : IntegrationEventConsumer<RolePermissionsChangedIntegrationEvent, OrganizationDbContext>(db, clock)
{
    protected override Task ConsumeAsync(RolePermissionsChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return cache.InvalidateOrganizationAsync(integrationEvent.OrganizationId, cancellationToken);
    }
}

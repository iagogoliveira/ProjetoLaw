using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LexFlow.Infrastructure.Common.Persistence.Interceptors;

/// <summary>
/// Último interceptor: para cada entidade inserida, alterada ou excluída, adiciona uma <see cref="AuditLogEntry"/>
/// ao mesmo <c>SaveChanges</c>, portanto no mesmo commit da alteração (seção 6.4). Campos <c>[NotAudited]</c> e
/// <c>[Sensitive]</c> nunca entram em <c>changes</c>; entidades com <c>[NotAudited]</c> na classe ficam fora da trilha.
/// </summary>
internal sealed class AuditSaveChangesInterceptor(
    IClock clock,
    IIdGenerator ids,
    ICurrentUser user,
    ITenantContext tenant,
    IRequestContext request) : SaveChangesInterceptor
{
    /// <summary>Colunas de controle: já estão na própria linha da trilha (<c>occurred_at</c>, <c>user_id</c>).</summary>
    private static readonly HashSet<string> BookkeepingProperties = new(StringComparer.Ordinal)
    {
        nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy), nameof(IAuditable.UpdatedAt), nameof(IAuditable.UpdatedBy),
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Entity is not AuditLogEntry && !e.Metadata.IsOwned())
                .Where(e => e.Metadata.ClrType.GetCustomAttribute<NotAuditedAttribute>() is null)
                .Select(ToAuditEntry)
                .OfType<AuditLogEntry>()
                .ToList();

            context.Set<AuditLogEntry>().AddRange(entries);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private AuditLogEntry? ToAuditEntry(EntityEntry entry)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditActions.Created,
            EntityState.Deleted => AuditActions.Deleted,
            _ when entry.Entity is ISoftDeletable && IsBeingSoftDeleted(entry) => AuditActions.Deleted,
            _ => AuditActions.Updated,
        };

        var changes = new JsonObject();
        foreach (var property in entry.Properties.Where(IsAuditable))
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(property.Metadata.Name);
            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = new JsonObject { ["new"] = ToJson(property.CurrentValue) };
                    break;
                case EntityState.Deleted:
                    changes[name] = new JsonObject { ["old"] = ToJson(property.OriginalValue) };
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[name] = new JsonObject { ["old"] = ToJson(property.OriginalValue), ["new"] = ToJson(property.CurrentValue) };
                    break;
            }
        }

        if (entry.State == EntityState.Modified && changes.Count == 0)
        {
            return null;
        }

        var key = entry.Metadata.FindPrimaryKey()!.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString());
        return new AuditLogEntry
        {
            Id = ids.NewId(),
            OrganizationId = entry.Entity is ITenantOwned owned ? owned.OrganizationId : tenant.OrganizationId,
            UserId = user.UserId,
            Action = action,
            EntityType = entry.Metadata.ClrType.Name,
            EntityId = string.Join('|', key),
            Changes = changes.ToJsonString(JsonOptions),
            Ip = request.IpAddress,
            UserAgent = request.UserAgent,
            CorrelationId = request.CorrelationId,
            OccurredAt = clock.UtcNow,
        };
    }

    private static bool IsBeingSoftDeleted(EntityEntry entry)
    {
        var deleted = entry.Property(nameof(ISoftDeletable.IsDeleted));
        return deleted.IsModified && deleted.CurrentValue is true && deleted.OriginalValue is false;
    }

    /// <summary>Exclui token de concorrência, colunas de controle e propriedades marcadas como sensíveis ou não auditadas.</summary>
    private static bool IsAuditable(PropertyEntry property)
    {
        if (property.Metadata.IsShadowProperty() || property.Metadata.IsConcurrencyToken || BookkeepingProperties.Contains(property.Metadata.Name))
        {
            return false;
        }

        var member = property.Metadata.PropertyInfo;
        return member is null
            || (member.GetCustomAttribute<NotAuditedAttribute>() is null && member.GetCustomAttribute<SensitiveAttribute>() is null);
    }

    private static JsonNode? ToJson(object? value) => value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType(), JsonOptions);
}

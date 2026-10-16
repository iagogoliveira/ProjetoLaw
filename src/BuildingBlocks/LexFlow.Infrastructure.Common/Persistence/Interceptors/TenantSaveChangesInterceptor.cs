using LexFlow.Application.Abstractions;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LexFlow.Infrastructure.Common.Persistence.Interceptors;

/// <summary>Tentativa de gravar dado de escritório fora da organização ativa (seção 6.2, item 4).</summary>
public sealed class TenantViolationException(string message) : InvalidOperationException(message);

/// <summary>
/// Preenche <c>OrganizationId</c> nos inserts e impede que uma entidade de outra organização seja inserida,
/// alterada ou excluída, ou que uma entidade troque de organização.
/// </summary>
internal sealed class TenantSaveChangesInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            Apply(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            var organization = tenant.OrganizationId
                ?? throw new TenantViolationException($"Gravação de {entry.Metadata.ClrType.Name} sem organização ativa.");
            var property = entry.Property(e => e.OrganizationId);

            if (entry.State == EntityState.Added)
            {
                if (property.CurrentValue == Guid.Empty)
                {
                    property.CurrentValue = organization;
                }
                else if (property.CurrentValue != organization)
                {
                    throw new TenantViolationException($"{entry.Metadata.ClrType.Name} pertence a outra organização.");
                }

                continue;
            }

            if (property.OriginalValue != organization || property.CurrentValue != organization)
            {
                throw new TenantViolationException($"{entry.Metadata.ClrType.Name} pertence a outra organização ou teve a organização alterada.");
            }
        }
    }
}

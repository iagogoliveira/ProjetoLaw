using LexFlow.Application.Abstractions;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LexFlow.Infrastructure.Common.Persistence.Interceptors;

/// <summary>Usuário gravado em <c>created_by</c>/<c>updated_by</c> quando a operação é do próprio sistema (job sem usuário).</summary>
public static class SystemUser
{
    public static readonly Guid Id = Guid.Empty;
}

/// <summary>
/// Preenche <c>created_at/by</c> e <c>updated_at/by</c> (<see cref="IAuditable"/>) e transforma exclusões de
/// <see cref="ISoftDeletable"/> em exclusão lógica (<c>is_deleted</c>, <c>deleted_at</c>).
/// </summary>
internal sealed class AuditableEntitiesInterceptor(IClock clock, ICurrentUser user) : SaveChangesInterceptor
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
        var now = clock.UtcNow;
        var userId = user.UserId ?? SystemUser.Id;

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>().Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Property(e => e.IsDeleted).CurrentValue = true;
            entry.Property(e => e.DeletedAt).CurrentValue = now;
        }

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedAt).CurrentValue = now;
                    entry.Property(e => e.CreatedBy).CurrentValue = userId;
                    break;
                case EntityState.Modified:
                    entry.Property(e => e.UpdatedAt).CurrentValue = now;
                    entry.Property(e => e.UpdatedBy).CurrentValue = userId;
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    break;
            }
        }
    }
}

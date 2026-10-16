using System.Linq.Expressions;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;

namespace LexFlow.Infrastructure.Common.Persistence;

/// <summary>
/// Base do DbContext de cada módulo (seção 6): schema próprio, filtros globais nomeados de tenant e de exclusão
/// lógica, <c>xmin</c> como token de concorrência e a trilha <c>audit.audit_logs</c> mapeada para gravação na mesma transação.
/// </summary>
public abstract class ModuleDbContextBase(DbContextOptions options, ITenantContext tenant) : DbContext(options), IUnitOfWork
{
    /// <summary>Nome do filtro de tenant (EF 10, filtros nomeados).</summary>
    public const string TenantFilter = "tenant";

    /// <summary>Nome do filtro de exclusão lógica.</summary>
    public const string SoftDeleteFilter = "soft_delete";

    /// <summary>Schema do módulo (ex.: <c>litigation</c>).</summary>
    protected abstract string Schema { get; }

    /// <summary>Só o módulo Platform cria a tabela de auditoria nas migrations; os demais apenas gravam nela.</summary>
    protected virtual bool OwnsAuditLog => false;

    /// <summary>Organização usada pelo filtro global; nula = nenhuma linha de escritório é visível.</summary>
    internal Guid? CurrentOrganizationId => tenant.OrganizationId;

    public bool HasActiveTransaction => Database.CurrentTransaction is not null;

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new EfTransaction(await Database.BeginTransactionAsync(cancellationToken));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        modelBuilder.ApplyConfiguration(new AuditLogEntryConfiguration(excludeFromMigrations: !OwnsAuditLog));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(e => !e.IsOwned() && e.BaseType is null).ToList())
        {
            var clrType = entityType.ClrType;
            var builder = modelBuilder.Entity(clrType);

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                builder.HasQueryFilter(TenantFilter, TenantFilterFor(clrType));
                builder.Property<uint>("Version").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                builder.HasQueryFilter(SoftDeleteFilter, SoftDeleteFilterFor(clrType));
                builder.Property(nameof(ISoftDeletable.IsDeleted)).HasDefaultValue(false);
            }
        }
    }

    /// <summary><c>e =&gt; e.OrganizationId == this.CurrentOrganizationId</c> (parametrizado por consulta pelo EF).</summary>
    private LambdaExpression TenantFilterFor(Type clrType)
    {
        var entity = Expression.Parameter(clrType, "e");
        var organizationId = Expression.Convert(PropertyOf<Guid>(entity, nameof(ITenantOwned.OrganizationId)), typeof(Guid?));
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId));
        return Expression.Lambda(Expression.Equal(organizationId, current), entity);
    }

    private static LambdaExpression SoftDeleteFilterFor(Type clrType)
    {
        var entity = Expression.Parameter(clrType, "e");
        return Expression.Lambda(Expression.Not(PropertyOf<bool>(entity, nameof(ISoftDeletable.IsDeleted))), entity);
    }

    private static MethodCallExpression PropertyOf<T>(ParameterExpression entity, string name) =>
        Expression.Call(typeof(EF), nameof(EF.Property), [typeof(T)], entity, Expression.Constant(name));

    private sealed class EfTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

/// <summary>Configuração padrão das colunas de <see cref="IAuditable"/> e <see cref="ISoftDeletable"/> (seção 6.1).</summary>
public static class EntityTypeBuilderExtensions
{
    public static EntityTypeBuilder<T> HasStandardColumns<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (typeof(IAuditable).IsAssignableFrom(typeof(T)))
        {
            builder.Property(nameof(IAuditable.CreatedAt)).IsRequired();
            builder.Property(nameof(IAuditable.CreatedBy)).IsRequired();
        }

        return builder;
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Data;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Common.Persistence;

/// <summary>
/// Base do DbContext de cada módulo (seção 6): aplica as convenções de <see cref="ModuleModel"/> (schema próprio,
/// filtros de tenant e de exclusão lógica, <c>xmin</c>, trilha de auditoria e outbox na mesma transação).
/// </summary>
public abstract class ModuleDbContextBase(DbContextOptions options, ITenantContext tenant) : DbContext(options), IModuleDbContext
{
    /// <summary>Nome do filtro de tenant (EF 10, filtros nomeados).</summary>
    public const string TenantFilter = ModuleModel.TenantFilter;

    /// <summary>Nome do filtro de exclusão lógica.</summary>
    public const string SoftDeleteFilter = ModuleModel.SoftDeleteFilter;

    /// <summary>Schema das tabelas do Wolverine (outbox/inbox).</summary>
    public const string WolverineSchema = ModuleModel.WolverineSchema;

    /// <summary>Schema do módulo (ex.: <c>litigation</c>).</summary>
    protected abstract string Schema { get; }

    /// <summary>Só o módulo Platform cria a tabela de auditoria nas migrations; os demais apenas gravam nela.</summary>
    protected virtual bool OwnsAuditLog => false;

    public Guid? CurrentOrganizationId => tenant.OrganizationId;

    public bool HasActiveTransaction => Database.CurrentTransaction is not null;

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        ModuleModel.BeginTransactionAsync(Database, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        ModuleModel.Apply(modelBuilder, this, Schema, OwnsAuditLog, GetType().Assembly);
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

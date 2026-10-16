using System.Linq.Expressions;
using System.Reflection;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Wolverine.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Common.Persistence;

/// <summary>
/// DbContext de módulo: expõe a organização usada pelos filtros e a transação do módulo. Implementado por
/// <see cref="ModuleDbContextBase"/> e por contextos com outra classe base (ex.: o do Identity, sobre o <c>IdentityDbContext</c>).
/// </summary>
public interface IModuleDbContext : IUnitOfWork
{
    /// <summary>Organização usada pelo filtro global; nula = nenhuma linha de escritório é visível.</summary>
    Guid? CurrentOrganizationId { get; }
}

/// <summary>Convenções de modelo comuns a todos os DbContexts de módulo (seção 6).</summary>
public static class ModuleModel
{
    /// <summary>Nome do filtro de tenant (EF 10, filtros nomeados).</summary>
    public const string TenantFilter = "tenant";

    /// <summary>Nome do filtro de exclusão lógica.</summary>
    public const string SoftDeleteFilter = "soft_delete";

    /// <summary>Schema das tabelas do Wolverine (outbox/inbox), criadas e mantidas pelo próprio Wolverine.</summary>
    public const string WolverineSchema = "wolverine";

    /// <summary>
    /// Aplica: schema do módulo, configurações do assembly, trilha de auditoria (criada só pelo dono), registro de
    /// eventos processados, envelopes do Wolverine (fora das migrations), filtros de tenant e de exclusão lógica e
    /// <c>xmin</c> como token de concorrência das entidades de escritório.
    /// </summary>
    public static void Apply(ModelBuilder modelBuilder, IModuleDbContext context, string schema, bool ownsAuditLog, Assembly configurationsAssembly)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(context);

        modelBuilder.HasDefaultSchema(schema);
        modelBuilder.ApplyConfigurationsFromAssembly(configurationsAssembly);
        modelBuilder.ApplyConfiguration(new AuditLogEntryConfiguration(excludeFromMigrations: !ownsAuditLog));
        modelBuilder.ApplyConfiguration(new ProcessedIntegrationEventConfiguration());
        MapWolverineEnvelopes(modelBuilder);

        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.BaseType is null && !string.Equals(e.GetSchema(), WolverineSchema, StringComparison.Ordinal))
            .ToList();
        foreach (var entityType in entityTypes)
        {
            var clrType = entityType.ClrType;
            var builder = modelBuilder.Entity(clrType);

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                builder.HasQueryFilter(TenantFilter, TenantFilterFor(clrType, context));
                builder.Property<uint>("Version").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                builder.HasQueryFilter(SoftDeleteFilter, SoftDeleteFilterFor(clrType));
                builder.Property(nameof(ISoftDeletable.IsDeleted)).HasDefaultValue(false);
            }
        }
    }

    public static async Task<IUnitOfWorkTransaction> BeginTransactionAsync(DatabaseFacade database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        return new EfTransaction(await database.BeginTransactionAsync(cancellationToken));
    }

    /// <summary>
    /// Mapeia as tabelas de envelopes do Wolverine para que o outbox grave as mensagens no mesmo <c>SaveChanges</c>.
    /// Ficam fora das migrations e da auditoria: o Wolverine cria e evolui o próprio schema.
    /// </summary>
    private static void MapWolverineEnvelopes(ModelBuilder modelBuilder)
    {
        var before = modelBuilder.Model.GetEntityTypes().Select(e => e.ClrType).ToHashSet();
        modelBuilder.MapWolverineEnvelopeStorage(WolverineSchema);
        foreach (var added in modelBuilder.Model.GetEntityTypes().Where(e => !before.Contains(e.ClrType)).ToList())
        {
            modelBuilder.Entity(added.ClrType).ToTable(added.GetTableName()!, added.GetSchema(), t => t.ExcludeFromMigrations());
        }
    }

    /// <summary><c>e =&gt; e.OrganizationId == context.CurrentOrganizationId</c> (parametrizado por consulta pelo EF).</summary>
    private static LambdaExpression TenantFilterFor(Type clrType, IModuleDbContext context)
    {
        var entity = Expression.Parameter(clrType, "e");
        var organizationId = Expression.Convert(PropertyOf<Guid>(entity, nameof(ITenantOwned.OrganizationId)), typeof(Guid?));
        var current = Expression.Property(Expression.Constant(context), context.GetType(), nameof(IModuleDbContext.CurrentOrganizationId));
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

/// <summary>
/// Marcações de auditoria por configuração do EF, para tipos que não podemos anotar com atributos
/// (ex.: <c>IdentityUser.PasswordHash</c>, tabelas do OpenIddict).
/// </summary>
public static class AuditAnnotations
{
    public const string Sensitive = "LexFlow:Sensitive";
    public const string NotAudited = "LexFlow:NotAudited";

    /// <summary>Equivale a <c>[Sensitive]</c>: o valor nunca entra em <c>changes</c>.</summary>
    public static PropertyBuilder IsSensitive(this PropertyBuilder property)
    {
        ArgumentNullException.ThrowIfNull(property);
        return property.HasAnnotation(Sensitive, true);
    }

    /// <summary>Equivale a <c>[NotAudited]</c> na classe: a entidade fica fora da trilha.</summary>
    public static EntityTypeBuilder IsNotAudited(this EntityTypeBuilder entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return entity.HasAnnotation(NotAudited, true);
    }
}

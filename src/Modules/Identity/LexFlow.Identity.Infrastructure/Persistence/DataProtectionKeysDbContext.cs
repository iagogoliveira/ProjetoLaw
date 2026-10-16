using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Identity.Infrastructure.Persistence;

/// <summary>
/// Acesso ao anel de chaves do Data Protection (<c>identity.data_protection_keys</c>), compartilhado por API e Worker
/// para que um aceite os tokens gerados pelo outro. Contexto à parte, sem os interceptors de módulo: o repositório do
/// Data Protection grava com <c>SaveChanges</c> síncrono, e chaves não entram na auditoria nem no outbox. A tabela é
/// criada pelas migrations do <see cref="IdentityModuleDbContext"/> (em <c>dotnet ef</c>, use <c>--context IdentityModuleDbContext</c>).
/// </summary>
public sealed class DataProtectionKeysDbContext(DbContextOptions<DataProtectionKeysDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(IdentityModuleDbContext.SchemaName);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys", t => t.ExcludeFromMigrations());
    }
}

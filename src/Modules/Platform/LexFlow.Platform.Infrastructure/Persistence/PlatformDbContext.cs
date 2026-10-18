using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Domain.Calendar;
using LexFlow.Platform.Domain.Courts;
using LexFlow.Platform.Domain.CustomFields;
using LexFlow.Platform.Domain.Idempotency;
using LexFlow.Platform.Domain.Indexes;
using LexFlow.Platform.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Infrastructure.Persistence;

/// <summary>DbContext do módulo Platform (schema <c>platform</c>); dono da tabela <c>audit.audit_logs</c>.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ITenantContext tenant)
    : ModuleDbContextBase(options, tenant), IPlatformDbContext
{
    public const string SchemaName = "platform";
    public const string ModuleName = "Platform";

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    public DbSet<Holiday> Holidays => Set<Holiday>();

    public DbSet<Court> Courts => Set<Court>();

    public DbSet<CustomFieldDefinition> CustomFields => Set<CustomFieldDefinition>();

    public DbSet<LookupTable> LookupTables => Set<LookupTable>();

    public DbSet<LookupItem> LookupItems => Set<LookupItem>();

    public DbSet<MonetaryIndex> MonetaryIndexes => Set<MonetaryIndex>();

    protected override string Schema => SchemaName;

    protected override bool OwnsAuditLog => true;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Extensões habilitadas na primeira migration (seção 6.1).
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("vector");

        base.OnModelCreating(modelBuilder);
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Scheduling.Application;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Scheduling.Infrastructure.Persistence;

public sealed class SchedulingDbContext(DbContextOptions<SchedulingDbContext> options, ITenantContext tenant)
    : ModuleDbContextBase(options, tenant), ISchedulingDbContext
{
    public const string SchemaName = "scheduling";
    public const string ModuleName = "Scheduling";
    protected override string Schema => SchemaName;
    public DbSet<Deadline> Deadlines => Set<Deadline>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<CalendarEntry> CalendarEntries => Set<CalendarEntry>();
    public DbSet<DeadlineActivity> DeadlineActivities => Set<DeadlineActivity>();

    public async Task LockDeadlinesAsync(CancellationToken cancellationToken)
    {
        if (!HasActiveTransaction || CurrentOrganizationId is null) { throw new InvalidOperationException("O ciclo do prazo exige tenant e transação."); }
        var key = "scheduling:" + CurrentOrganizationId.Value.ToString("N");
        await Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
    }
}

internal sealed class WorkConfigurations : IEntityTypeConfiguration<Deadline>, IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<Deadline> builder)
    {
        builder.ToTable("deadlines");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Title).HasMaxLength(300);
        builder.Property(d => d.Type).HasMaxLength(80).HasDefaultValue("procedural");
        builder.Property(d => d.CalculationSnapshot).HasColumnType("jsonb");
        builder.Property(d => d.CompletionEvidence).HasMaxLength(4000);
        builder.Property(d => d.MissedReason).HasMaxLength(4000);
        builder.Property(d => d.StateCode).HasMaxLength(2);
        builder.Property(d => d.Municipality).HasMaxLength(120);
        builder.OwnsOne(d => d.Rule, rule =>
        {
            rule.Property(r => r.StartKind).HasConversion<string>().HasMaxLength(30);
            rule.Property(r => r.Mode).HasConversion<string>().HasMaxLength(20);
        });
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(d => new { d.OrganizationId, d.PublicationId }).IsUnique();
        builder.HasIndex(d => new { d.OrganizationId, d.LegalCaseId, d.FatalDueOn });
    }
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).HasMaxLength(300);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(t => new { t.OrganizationId, t.PublicationId }).IsUnique();
        builder.HasIndex(t => new { t.OrganizationId, t.LegalCaseId, t.DueOn });
    }
}

internal sealed class DeadlineActivityConfiguration : IEntityTypeConfiguration<DeadlineActivity>
{
    public void Configure(EntityTypeBuilder<DeadlineActivity> builder)
    {
        builder.ToTable("deadline_activities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Kind).HasMaxLength(80);
        builder.HasOne<Deadline>().WithMany().HasForeignKey(a => a.DeadlineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.OrganizationId, a.LegalCaseId, a.OccurredAt });
    }
}

internal sealed class CalendarEntryConfiguration : IEntityTypeConfiguration<CalendarEntry>
{
    public void Configure(EntityTypeBuilder<CalendarEntry> builder)
    {
        builder.ToTable("calendar_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.StateCode).HasMaxLength(2).IsFixedLength();
        builder.Property(e => e.Municipality).HasMaxLength(120);
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.HasIndex(e => new { e.OrganizationId, e.StartsOn });
    }
}

internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SchedulingDbContext>
{
    public SchedulingDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SchedulingDbContext>().UseNpgsql("Host=localhost;Database=lexflow_design",
            n => n.MigrationsHistoryTable("__ef_migrations_history", SchedulingDbContext.SchemaName)).UseSnakeCaseNamingConvention().Options, new NoTenant());
    private sealed class NoTenant : ITenantContext { public Guid? OrganizationId => null; }
}

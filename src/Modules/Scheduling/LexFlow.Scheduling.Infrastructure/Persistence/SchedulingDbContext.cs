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
}

internal sealed class WorkConfigurations : IEntityTypeConfiguration<Deadline>, IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<Deadline> builder)
    {
        builder.ToTable("deadlines");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Title).HasMaxLength(300);
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

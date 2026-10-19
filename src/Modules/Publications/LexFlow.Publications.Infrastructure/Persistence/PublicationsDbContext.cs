using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Publications.Application;
using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LexFlow.Publications.Infrastructure.Persistence;

public sealed class PublicationsDbContext(DbContextOptions<PublicationsDbContext> options, ITenantContext tenant)
    : ModuleDbContextBase(options, tenant), IPublicationsDbContext
{
    public const string SchemaName = "publications";
    public const string ModuleName = "Publications";
    protected override string Schema => SchemaName;
    public DbSet<CaptureSource> Sources => Set<CaptureSource>();
    public DbSet<CaptureRun> Runs => Set<CaptureRun>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PublicationTriage> Triages => Set<PublicationTriage>();
    public DbSet<CaseMovement> Movements => Set<CaseMovement>();
}

internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<PublicationsDbContext>
{
    public PublicationsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PublicationsDbContext>()
            .UseNpgsql("Host=localhost;Database=lexflow_design", n => n.MigrationsHistoryTable("__ef_migrations_history", PublicationsDbContext.SchemaName))
            .UseSnakeCaseNamingConvention().Options;
        return new PublicationsDbContext(options, new NoTenant());
    }
    private sealed class NoTenant : ITenantContext
    {
        public Guid? OrganizationId => null;
    }
}

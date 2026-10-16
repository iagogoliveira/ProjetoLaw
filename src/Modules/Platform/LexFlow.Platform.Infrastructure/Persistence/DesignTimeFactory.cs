using LexFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LexFlow.Platform.Infrastructure.Persistence;

/// <summary>
/// Usado só pelo <c>dotnet ef migrations add</c> para montar o modelo. A connection string não tem senha e nunca é
/// usada para conectar (migrations são aplicadas pelo MigrationService ou pelo bundle no pipeline).
/// </summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql("Host=localhost;Database=lexflow_design", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", PlatformDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PlatformDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? OrganizationId => null;
    }
}

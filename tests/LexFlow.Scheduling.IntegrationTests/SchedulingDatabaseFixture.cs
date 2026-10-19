using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Scheduling.Application;
using LexFlow.Scheduling.Infrastructure;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(LexFlow.Scheduling.IntegrationTests.SchedulingDatabaseFixture))]

namespace LexFlow.Scheduling.IntegrationTests;

/// <summary>PostgreSQL com as migrations reais do Platform (extensões e auditoria) e do Scheduling aplicadas do zero.</summary>
public sealed class SchedulingDatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("pgvector/pgvector:pg17-trixie").Build();

    public string ConnectionString => Postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Postgres.StartAsync();
        await MigrateAsync(ConnectionString);
    }

    public static async Task MigrateAsync(string connectionString)
    {
        await using var provider = BuildProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Database.MigrateAsync();
    }

    public static ServiceProvider BuildProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["ConnectionStrings:lexflow"] = connectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSystemContexts();
        services.AddPlatformInfrastructure(configuration);
        services.AddSchedulingPersistence(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = "lexflow_" + Guid.CreateVersion7().ToString("N");
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public async ValueTask DisposeAsync() => await Postgres.DisposeAsync();
}

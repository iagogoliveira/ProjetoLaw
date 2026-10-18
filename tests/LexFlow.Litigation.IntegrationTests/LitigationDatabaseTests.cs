using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Infrastructure;
using LexFlow.Litigation.Infrastructure.Persistence;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(LexFlow.Litigation.IntegrationTests.LitigationDatabaseFixture))]

namespace LexFlow.Litigation.IntegrationTests;

/// <summary>PostgreSQL com as migrations reais do Platform (extensões e auditoria) e do Litigation aplicadas do zero.</summary>
public sealed class LitigationDatabaseFixture : IAsyncLifetime
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
        await scope.ServiceProvider.GetRequiredService<LitigationDbContext>().Database.MigrateAsync();
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
        services.AddLitigationPersistence(configuration);
        services.AddScoped<ICaseCounter, CaseCounterStore>();
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

/// <summary>Migrations do módulo e o contador da numeração interna (atômico, por escritório, área e ano).</summary>
public sealed class LitigationDatabaseTests(LitigationDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    /// <summary>Próximo número numa transação, como no caso de uso de cadastro.</summary>
    private async Task<long> NextAsync(Guid organization, Guid? area, int year, bool commit = true)
    {
        using var ambient = AmbientContext.Begin(organization, "litigation-tests");
        await using var provider = LitigationDatabaseFixture.BuildProvider(fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LitigationDbContext>();
        await using var transaction = await db.BeginTransactionAsync(Ct);
        var next = await scope.ServiceProvider.GetRequiredService<ICaseCounter>().NextAsync(area, year, Ct);
        if (commit)
        {
            await transaction.CommitAsync(Ct);
        }
        else
        {
            await transaction.RollbackAsync(Ct);
        }

        return next;
    }

    [Fact]
    public async Task Migrations_apply_from_scratch_and_revert()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await LitigationDatabaseFixture.MigrateAsync(connectionString);
        await using var provider = LitigationDatabaseFixture.BuildProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LitigationDbContext>();
        var migrator = db.GetService<IMigrator>();

        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        await migrator.MigrateAsync(Migration.InitialDatabase, cancellationToken: Ct);
        (await db.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
        await migrator.MigrateAsync(cancellationToken: Ct);
    }

    /// <summary>Decisão de 2026-10-18: a sequência é contada por área e ano, separada por escritório.</summary>
    [Fact]
    public async Task The_counter_runs_per_office_area_and_year_and_a_rollback_gives_the_number_back()
    {
        var (office, other) = (Guid.CreateVersion7(), Guid.CreateVersion7());
        var (labor, civil) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        (await NextAsync(office, labor, 2026)).ShouldBe(1);
        (await NextAsync(office, labor, 2026)).ShouldBe(2);
        (await NextAsync(office, civil, 2026)).ShouldBe(1);
        (await NextAsync(office, labor, 2027)).ShouldBe(1);
        (await NextAsync(office, null, 2026)).ShouldBe(1);
        (await NextAsync(other, labor, 2026)).ShouldBe(1);
        (await NextAsync(office, labor, 2026, commit: false)).ShouldBe(3);
        (await NextAsync(office, labor, 2026)).ShouldBe(3, "o número da transação desfeita volta");
    }

    [Fact]
    public async Task Concurrent_registrations_never_get_the_same_number()
    {
        var (office, area) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => NextAsync(office, area, 2026)));

        numbers.Order().ShouldBe(Enumerable.Range(1, 20).Select(n => (long)n));
    }

    /// <summary>O histórico de valores dos pedidos (RF-13) só recebe inserções: a aplicação não altera nem apaga.</summary>
    [Theory]
    [InlineData("UPDATE litigation.claim_value_changes SET new_amount = 0")]
    [InlineData("DELETE FROM litigation.claim_value_changes")]
    public async Task The_application_cannot_rewrite_the_claim_value_history(string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using (var role = new NpgsqlCommand("SET ROLE lexflow_app", connection))
        {
            await role.ExecuteNonQueryAsync(Ct);
        }

        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
        error.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }
}

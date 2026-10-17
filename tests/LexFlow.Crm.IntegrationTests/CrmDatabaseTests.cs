using LexFlow.Application.Abstractions;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain.People;
using LexFlow.Crm.Infrastructure;
using LexFlow.Crm.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(LexFlow.Crm.IntegrationTests.CrmDatabaseFixture))]

namespace LexFlow.Crm.IntegrationTests;

/// <summary>PostgreSQL com as migrations reais do Platform (extensões e auditoria) e do Crm aplicadas do zero.</summary>
public sealed class CrmDatabaseFixture : IAsyncLifetime
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
        await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.MigrateAsync();
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
        services.AddCrmPersistence(configuration);
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

/// <summary>Migrations do módulo, unicidade do documento no escritório e a busca aproximada do PostgreSQL.</summary>
public sealed class CrmDatabaseTests(CrmDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static Person NewPerson(string name, string? document = null, PersonKind kind = PersonKind.Company) =>
        Person.Create(Guid.CreateVersion7(), kind, new PersonDetails(name, null, document, null, null, null, null, null, [PersonRoleKind.Client], [], [])).Value;

    private async Task SaveAsync(Guid organizationId, params Person[] people)
    {
        using var ambient = AmbientContext.Begin(organizationId, "crm-tests");
        await using var provider = CrmDatabaseFixture.BuildProvider(fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        db.People.AddRange(people);
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Migrations_apply_from_scratch_and_revert()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await CrmDatabaseFixture.MigrateAsync(connectionString);
        await using var provider = CrmDatabaseFixture.BuildProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var migrator = db.GetService<IMigrator>();

        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        await migrator.MigrateAsync(Migration.InitialDatabase, cancellationToken: Ct);
        (await db.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
        await migrator.MigrateAsync(cancellationToken: Ct);
    }

    /// <summary>Critério de aceite da Etapa 4, no banco: CPF/CNPJ duplicado no mesmo escritório é bloqueado; em outro, não.</summary>
    [Fact]
    public async Task A_document_is_unique_inside_the_organization_only()
    {
        var first = Guid.CreateVersion7();
        await SaveAsync(first, NewPerson("Empresa A", "11222333000181"));
        await SaveAsync(Guid.CreateVersion7(), NewPerson("Empresa A em outro escritório", "11222333000181"));

        var duplicate = await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(first, NewPerson("Outra", "11222333000181")));

        duplicate.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        await SaveAsync(first, NewPerson("Sem documento"), NewPerson("Também sem documento"));
    }

    [Fact]
    public async Task Word_similarity_finds_a_name_without_accents_inside_a_longer_name()
    {
        var organization = Guid.CreateVersion7();
        await SaveAsync(organization, NewPerson("João da Silva Ltda."), NewPerson("Maria Souza"));
        using var ambient = AmbientContext.Begin(organization, "crm-tests");
        await using var provider = CrmDatabaseFixture.BuildProvider(fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var term = SearchText.Normalize("Joao da Silva");

        var scores = await db.People.AsNoTracking()
            .Select(p => new { p.Name, Score = CrmDbFunctions.WordSimilarity(term, p.SearchName) })
            .ToDictionaryAsync(p => p.Name, p => p.Score, Ct);

        scores["João da Silva Ltda."].ShouldBe(1, 0.001);
        scores["Maria Souza"].ShouldBeLessThan(0.5);
    }
}

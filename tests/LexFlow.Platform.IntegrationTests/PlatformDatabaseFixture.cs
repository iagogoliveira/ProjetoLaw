using LexFlow.Application.Abstractions;
using LexFlow.Platform.Application;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(LexFlow.Platform.IntegrationTests.PlatformDatabaseFixture))]

namespace LexFlow.Platform.IntegrationTests;

public sealed class TestTenant : ITenantContext
{
    public Guid? OrganizationId { get; set; }
}

public sealed class TestUser : ICurrentUser
{
    public Guid? UserId { get; set; }

    public bool IsAuthenticated => UserId.HasValue;
}

public sealed class TestRequest : IRequestContext
{
    public string? CorrelationId => "platform-tests";

    public string? IpAddress => "203.0.113.20";

    public string? UserAgent => "LexFlow.Platform.Tests";
}

/// <summary>PostgreSQL (mesma imagem do AppHost) com as migrations reais do Platform aplicadas do zero.</summary>
public sealed class PlatformDatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("pgvector/pgvector:pg17-trixie").Build();

    public string ConnectionString => Postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Postgres.StartAsync();
        await using var provider = BuildProvider(ConnectionString, new TestTenant(), new TestUser());
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
    }

    public static ServiceProvider BuildProvider(string connectionString, TestTenant tenant, TestUser user, FakeTimeProvider? time = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["ConnectionStrings:lexflow"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(time ?? new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero)));
        services.AddSingleton<ITenantContext>(tenant);
        services.AddSingleton<ICurrentUser>(user);
        services.AddSingleton<IRequestContext>(new TestRequest());
        services.AddApplication(PlatformApplication.Assembly);
        services.AddPlatformInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>Banco vazio no mesmo container, para testes que aplicam/revertem migrations sem afetar os demais.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = "lexflow_" + Guid.CreateVersion7().ToString("N");
        await ExecuteAsync($"CREATE DATABASE {name}");
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>Banco próprio já migrado, para testes que afetariam outros (ex.: limpeza entre organizações).</summary>
    public async Task<string> CreateMigratedDatabaseAsync()
    {
        var connectionString = await CreateEmptyDatabaseAsync();
        await using var provider = BuildProvider(connectionString, new TestTenant(), new TestUser());
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
        return connectionString;
    }

    public async Task ExecuteAsync(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        await command.ExecuteNonQueryAsync();
    }

    public Task<T?> ScalarAsync<T>(string sql, params object[] parameters) => ScalarOnAsync<T>(ConnectionString, sql, parameters);

    public static async Task<T?> ScalarOnAsync<T>(string connectionString, string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    public async ValueTask DisposeAsync() => await Postgres.DisposeAsync();
}

using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Sample.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(LexFlow.BuildingBlocks.IntegrationTests.PersistenceFixture))]

namespace LexFlow.BuildingBlocks.IntegrationTests;

/// <summary>Contexto de teste mutável: cada teste usa organizações próprias, então não há limpeza entre testes.</summary>
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
    public string? CorrelationId { get; set; } = "test-correlation";

    public string? IpAddress { get; set; } = "203.0.113.10";

    public string? UserAgent { get; set; } = "LexFlow.Tests";
}

/// <summary>PostgreSQL com pgvector (mesma imagem do AppHost) e o schema do módulo de exemplo.</summary>
public sealed class PersistenceFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("pgvector/pgvector:pg17-trixie").Build();

    public async ValueTask InitializeAsync()
    {
        await Postgres.StartAsync();
        await using var provider = BuildProvider(new TestTenant(), new TestUser(), NewClock());
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        await ExecuteAsync(db.Database.GenerateCreateScript());
    }

    /// <summary>Contêiner de DI equivalente ao da API, com tenant, usuário e relógio controlados pelo teste.</summary>
    /// <summary>Relógio próprio de cada teste (testes rodam em paralelo e não podem mover o relógio uns dos outros).</summary>
    public static FakeTimeProvider NewClock() => new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

    public ServiceProvider BuildProvider(TestTenant tenant, TestUser user, FakeTimeProvider time, TestRequest? request = null,
        Action<IServiceCollection>? configure = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:lexflow"] = Postgres.GetConnectionString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(time);
        services.AddSingleton<ITenantContext>(tenant);
        services.AddSingleton<ICurrentUser>(user);
        services.AddSingleton<IRequestContext>(request ?? new TestRequest());
        services.AddApplication(typeof(PersistenceFixture).Assembly);
        services.AddModuleDbContext<SampleDbContext>(configuration, "Sample", "sample");
        services.AddModuleDbContext<SecondarySampleDbContext>(configuration, "SecondarySample", "sample");
        services.TryAddSingleton<IConfiguration>(configuration);
        configure?.Invoke(services);
        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>SQL de infraestrutura de teste via Npgsql (fora do EF, que proíbe SQL bruto).</summary>
    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string sql, Guid parameter)
    {
        await using var connection = new NpgsqlConnection(Postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(parameter);
        return await command.ExecuteScalarAsync();
    }

    public async ValueTask DisposeAsync() => await Postgres.DisposeAsync();
}

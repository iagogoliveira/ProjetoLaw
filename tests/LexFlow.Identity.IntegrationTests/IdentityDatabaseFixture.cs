using System.Collections.Concurrent;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Application;
using LexFlow.Identity.Infrastructure;
using LexFlow.Identity.Infrastructure.Emails;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(LexFlow.Identity.IntegrationTests.IdentityDatabaseFixture))]

namespace LexFlow.Identity.IntegrationTests;

/// <summary>E-mails "enviados" pelo processo de teste, em memória.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

/// <summary>PostgreSQL com as migrations reais do Platform (trilha de auditoria) e do Identity aplicadas do zero.</summary>
public sealed class IdentityDatabaseFixture : IAsyncLifetime
{
    public const string PublicUrl = "https://identity.lexflow.test";

    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("pgvector/pgvector:pg17-trixie").Build();

    public string ConnectionString => Postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Postgres.StartAsync();
        await MigrateAsync(ConnectionString);
    }

    public static async Task MigrateAsync(string connectionString)
    {
        await using var provider = BuildProvider(connectionString, new FakeEmailSender());
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// Serviços como no Worker: Identity completo, sem usuário nem tenant (processo de sistema), e-mail falso e o
    /// consumidor resolvível. Cada chamada é um "processo" separado, com o próprio cache do anel de chaves.
    /// </summary>
    public static ServiceProvider BuildProvider(string connectionString, FakeEmailSender email)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ConnectionStrings:lexflow"] = connectionString,
                ["Identity:PublicUrl"] = PublicUrl,
                ["Identity:CheckBreachedPasswords"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(IdentityApplication.Assembly);
        services.AddSystemContexts();
        services.AddPlatformInfrastructure(configuration);
        services.AddIdentityInfrastructure(configuration);
        services.AddIdentitySeeding(configuration);
        services.AddSingleton<IEmailSender>(email);
        services.AddScoped<AccountEmailConsumer>();
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

    public async Task<T?> ScalarAsync<T>(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
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

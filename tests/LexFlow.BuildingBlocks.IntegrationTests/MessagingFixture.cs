using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Sample.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace LexFlow.BuildingBlocks.IntegrationTests;

/// <summary>
/// Host com Wolverine real (outbox/inbox no PostgreSQL, transporte RabbitMQ) publicando e consumindo no mesmo processo,
/// como API e Worker fariam em processos separados.
/// </summary>
public sealed class MessagingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17-trixie").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4.3-management").Build();

    public IHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:lexflow"] = _postgres.GetConnectionString();
        builder.Configuration["ConnectionStrings:rabbit"] = _rabbit.GetConnectionString();
        builder.Services.AddApplication(typeof(MessagingFixture).Assembly);
        builder.Services.AddSystemContexts();
        builder.Services.AddLexFlowMessaging(builder.Configuration, typeof(MessagingFixture).Assembly);
        builder.Services.AddModuleDbContext<SampleDbContext>(builder.Configuration, "Sample", "sample");
        builder.Services.AddModuleDbContext<SecondarySampleDbContext>(builder.Configuration, "SecondarySample", "sample");
        Host = builder.Build();

        await using (var scope = Host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(db.Database.GenerateCreateScript(), connection);
            await command.ExecuteNonQueryAsync();
        }

        await Host.StartAsync();
    }

    public async Task<long> CountAsync(string sql, params Guid[] parameters)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        return (long)(await command.ExecuteScalarAsync())!;
    }

    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await _rabbit.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

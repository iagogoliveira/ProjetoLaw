using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
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
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(LexFlow.Api.IntegrationTests.DependenciesFixture))]

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Sobe, uma vez por assembly, as mesmas dependências do AppHost (seção 8.1) em containers reais.
/// </summary>
public sealed class DependenciesFixture : IAsyncLifetime
{
    public const string StorageAccessKey = "lexflow-test";
    public const string StorageSecretKey = "lexflow-test-secret";
    private const int StoragePort = 9000;

    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder(TestImages.Postgres).Build();

    public RedisContainer Redis { get; } = new RedisBuilder(TestImages.Redis).Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder(TestImages.RabbitMq).Build();

    public IContainer ObjectStorage { get; } = new ContainerBuilder(TestImages.ObjectStorage)
        .WithPortBinding(StoragePort, assignRandomHostPort: true)
        .WithEnvironment("RUSTFS_ACCESS_KEY", StorageAccessKey)
        .WithEnvironment("RUSTFS_SECRET_KEY", StorageSecretKey)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(StoragePort).ForPath("/health")))
        .Build();

    public Uri ObjectStorageUrl => new($"http://{ObjectStorage.Hostname}:{ObjectStorage.GetMappedPublicPort(StoragePort)}");

    /// <summary>Configuração que a API recebe do AppHost, apontando para os containers de teste.</summary>
    public IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ConnectionStrings:lexflow"] = Postgres.GetConnectionString(),
        ["ConnectionStrings:redis"] = Redis.GetConnectionString(),
        ["ConnectionStrings:rabbit"] = RabbitMq.GetConnectionString(),
        ["Storage:ServiceUrl"] = ObjectStorageUrl.ToString(),
        ["Storage:AccessKey"] = StorageAccessKey,
        ["Storage:SecretKey"] = StorageSecretKey,
        ["Identity:PublicUrl"] = "https://identity.lexflow.test",

        // Os testes não dependem da internet; a verificação de vazamentos tem testes próprios com API falsa.
        ["Identity:CheckBreachedPasswords"] = "false",
    };

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(Postgres.StartAsync(), Redis.StartAsync(), RabbitMq.StartAsync(), ObjectStorage.StartAsync());

        // Mesmo papel do MigrationService: schema do banco antes da API subir.
        await using var provider = PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
    }

    /// <summary>Serviços do módulo Platform fora da API (para migrar e preparar dados de teste).</summary>
    public ServiceProvider PlatformServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(Settings.ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.Ordinal))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSystemContexts();
        services.AddPlatformInfrastructure(configuration);
        services.AddIdentityPersistence(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// "Worker" de teste: o mesmo registro do Identity que o Worker usa, em outro container de DI, com e-mail falso.
    /// Prova que um token gerado nele é aceito pela API (anel de chaves compartilhado pelo banco).
    /// </summary>
    public ServiceProvider WorkerServices(IEmailSender email)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(Settings.ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.Ordinal))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(IdentityApplication.Assembly);
        services.AddSystemContexts();
        services.AddIdentityInfrastructure(configuration);
        services.AddSingleton(email);
        services.AddScoped<AccountEmailConsumer>();
        return services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await Postgres.DisposeAsync();
        await Redis.DisposeAsync();
        await RabbitMq.DisposeAsync();
        await ObjectStorage.DisposeAsync();
    }
}

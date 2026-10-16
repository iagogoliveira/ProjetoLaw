using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
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
    };

    public async ValueTask InitializeAsync() =>
        await Task.WhenAll(Postgres.StartAsync(), Redis.StartAsync(), RabbitMq.StartAsync(), ObjectStorage.StartAsync());

    public async ValueTask DisposeAsync()
    {
        await Postgres.DisposeAsync();
        await Redis.DisposeAsync();
        await RabbitMq.DisposeAsync();
        await ObjectStorage.DisposeAsync();
    }
}

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace LexFlow.BuildingBlocks.IntegrationTests;

/// <summary>Containers reais dos fornecedores (mesmas imagens do AppHost) e um host com os adaptadores e o servidor Hangfire.</summary>
public sealed class AdaptersFixture : IAsyncLifetime
{
    public const string StorageAccessKey = "lexflow-test";
    public const string StorageSecretKey = "lexflow-test-secret";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17-trixie").Build();

    private readonly IContainer _storage = new ContainerBuilder("rustfs/rustfs:1.0.2")
        .WithPortBinding(9000, assignRandomHostPort: true)
        .WithEnvironment("RUSTFS_ACCESS_KEY", StorageAccessKey)
        .WithEnvironment("RUSTFS_SECRET_KEY", StorageSecretKey)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9000).ForPath("/health")))
        .Build();

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.31.6")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/livez")))
        .Build();

    // A imagem já traz as assinaturas; o clamd abre a porta 3310 depois de carregá-las.
    private readonly IContainer _clamav = new ContainerBuilder("clamav/clamav:1.5.4-debian")
        .WithPortBinding(3310, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310))
        .Build();

    public IHost Host { get; private set; } = null!;

    public Uri MailpitApi => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}");

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _storage.StartAsync(), _mailpit.StartAsync(), _clamav.StartAsync());

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:lexflow"] = _postgres.GetConnectionString();
        builder.Configuration["Storage:ServiceUrl"] = $"http://{_storage.Hostname}:{_storage.GetMappedPublicPort(9000)}";
        builder.Configuration["Storage:AccessKey"] = StorageAccessKey;
        builder.Configuration["Storage:SecretKey"] = StorageSecretKey;
        builder.Configuration["Storage:Bucket"] = "lexflow-tests";
        builder.Configuration["Email:Host"] = _mailpit.Hostname;
        builder.Configuration["Email:Port"] = _mailpit.GetMappedPublicPort(1025).ToString(System.Globalization.CultureInfo.InvariantCulture);
        builder.Configuration["ClamAv:Host"] = _clamav.Hostname;
        builder.Configuration["ClamAv:Port"] = _clamav.GetMappedPublicPort(3310).ToString(System.Globalization.CultureInfo.InvariantCulture);

        builder.Services.AddApplication();
        builder.Services.AddSystemContexts();
        builder.Services.AddExternalAdapters(builder.Configuration);
        builder.Services.AddJobScheduling(builder.Configuration, runServer: true);
        builder.Services.AddTransient<RecordingJob>();
        Host = builder.Build();

        await Host.Services.GetRequiredService<IObjectStorageProvisioner>().EnsureBucketAsync(TestContext.Current.CancellationToken);
        await Host.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _storage.DisposeAsync().AsTask(), _mailpit.DisposeAsync().AsTask(), _clamav.DisposeAsync().AsTask());
    }
}

/// <summary>Job de teste: registra o tenant e a correlação com que foi executado.</summary>
public sealed class RecordingJob(ITenantContext tenant, IRequestContext request)
{
    public static System.Collections.Concurrent.ConcurrentDictionary<string, (Guid? Organization, string? Correlation)> Runs { get; } = new();

    public Task RunAsync(string marker)
    {
        Runs[marker] = (tenant.OrganizationId, request.CorrelationId);
        return Task.CompletedTask;
    }
}

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Crm.Infrastructure;
using LexFlow.Crm.Infrastructure.Persistence;
using LexFlow.Identity.Application;
using LexFlow.Identity.Infrastructure;
using LexFlow.Identity.Infrastructure.Emails;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Infrastructure;
using LexFlow.Litigation.Infrastructure.Persistence;
using LexFlow.Organization.Infrastructure;
using LexFlow.Organization.Infrastructure.Invitations;
using LexFlow.Organization.Infrastructure.Persistence;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.CustomFields;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Publications.Infrastructure;
using LexFlow.Publications.Infrastructure.Persistence;
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

    /// <summary>Login da aplicação nos testes, criado como no MigrationService (ADR 0019).</summary>
    public const string ApplicationLoginName = "lexflow_api_test";

    private readonly string _applicationPassword = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

    /// <summary>Dono do banco (superusuário do container): migrations e leituras de verificação acima da RLS.</summary>
    public string AdminConnectionString => Postgres.GetConnectionString();

    /// <summary>Como a API e o Worker conectam: login membro de <c>lexflow_app</c>, sujeito à RLS e aos privilégios.</summary>
    public string ApplicationConnectionString =>
        new Npgsql.NpgsqlConnectionStringBuilder(AdminConnectionString) { Username = ApplicationLoginName, Password = _applicationPassword }.ConnectionString;

    /// <summary>Configuração que a API recebe do AppHost, apontando para os containers de teste.</summary>
    public IReadOnlyDictionary<string, string> Settings => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ConnectionStrings:lexflow"] = ApplicationConnectionString,
        ["ConnectionStrings:redis"] = Redis.GetConnectionString(),
        ["ConnectionStrings:rabbit"] = RabbitMq.GetConnectionString(),
        ["Storage:ServiceUrl"] = ObjectStorageUrl.ToString(),
        ["Storage:AccessKey"] = StorageAccessKey,
        ["Storage:SecretKey"] = StorageSecretKey,
        ["Identity:PublicUrl"] = "https://identity.lexflow.test",

        // Os testes não dependem da internet; a verificação de vazamentos tem testes próprios com API falsa.
        ["Identity:CheckBreachedPasswords"] = "false",

        // Chaves OIDC só em memória (cada fábrica tem as suas) e limite por IP alto: no TestServer todos os pedidos
        // vêm do mesmo "IP". O limite tem teste próprio.
        ["Identity:Server:UseEphemeralKeys"] = "true",
        ["Identity:PublicPermitsPerMinute"] = "10000",

        // Consulta de CNPJ falsa e determinística: os testes não dependem da BrasilAPI.
        ["Crm:CnpjLookup:Provider"] = "Fake",

        // Índices monetários: fonte falsa, sem internet (o seed real vem da migration).
        ["Platform:MonetaryIndexes:Source"] = "Fake",
    };

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(Postgres.StartAsync(), Redis.StartAsync(), RabbitMq.StartAsync(), ObjectStorage.StartAsync());

        // Mesmo papel do MigrationService: schema do banco (como dono), login da aplicação e cliente lexflow-web.
        await using (var provider = ServicesFor(AdminConnectionString))
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<LitigationDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<PublicationsDbContext>().Database.MigrateAsync();
            await using (var connection = new Npgsql.NpgsqlConnection(AdminConnectionString))
            {
                await connection.OpenAsync();
                await ApplicationLogin.EnsureAsync(connection, ApplicationLoginName, _applicationPassword, default);
            }

            foreach (var seeder in scope.ServiceProvider.GetServices<IDatabaseSeeder>())
            {
                await seeder.SeedAsync(default);
            }
        }
    }

    /// <summary>Serviços dos módulos fora da API, conectando como a aplicação (sujeitos à RLS), para preparar dados de teste.</summary>
    public ServiceProvider PlatformServices() => ServicesFor(ApplicationConnectionString);

    private ServiceProvider ServicesFor(string connectionString)
    {
        var values = Settings.ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.Ordinal);
        values["ConnectionStrings:lexflow"] = connectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSystemContexts();
        services.AddPlatformInfrastructure(configuration);
        services.AddIdentityPersistence(configuration);
        services.AddIdentitySeeding(configuration);
        services.AddOrganizationInfrastructure(configuration);
        services.AddCrmPersistence(configuration);
        services.AddLitigationPersistence(configuration);
        services.AddPublicationsPersistence(configuration);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// "Worker" de teste: o mesmo registro do Identity e do Organization que o Worker usa, em outro container de DI,
    /// com e-mail falso. Prova que um token gerado nele é aceito pela API (anel de chaves compartilhado pelo banco, e o
    /// hash dos convites gravado pelo próprio consumidor).
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
        services.AddPlatformInfrastructure(configuration);
        services.AddIdentityInfrastructure(configuration);
        services.AddOrganizationInfrastructure(configuration);
        services.AddSingleton(email);
        services.AddScoped<AccountEmailConsumer>();
        services.AddScoped<InvitationEmailConsumer>();
        services.AddScoped<OrganizationCreatedConsumer>();
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

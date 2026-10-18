// Ambiente local completo (seção 8.1): `dotnet run --project src/Aspire/LexFlow.AppHost`.
using LexFlow.AppHost;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Testes do AppHost desligam volumes para começar sempre de um estado limpo.
var useDataVolumes = builder.Configuration.GetValue("LexFlow:UseDataVolumes", defaultValue: true);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17-trixie");
if (useDataVolumes)
{
    postgres.WithDataVolume();
}

var database = postgres.AddDatabase("lexflow");

// A API e o Worker conectam com um login próprio, membro de lexflow_app (sem superusuário nem BYPASSRLS): é isso que
// faz a Row-Level Security valer (ADR 0019). O MigrationService, que roda como dono, cria o login com esta senha.
const string appLogin = "lexflow_api";
var appPassword = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, "app-db-password", special: false);
var postgresEndpoint = postgres.Resource.PrimaryEndpoint;
var appConnection = ReferenceExpression.Create(
    $"Host={postgresEndpoint.Property(EndpointProperty.Host)};Port={postgresEndpoint.Property(EndpointProperty.Port)};Database=lexflow;Username={appLogin};Password={appPassword}");

var redis = builder.AddRedis("redis")
    .WithImageTag("8.8");

var rabbit = builder.AddRabbitMQ("rabbit")
    .WithManagementPlugin();

// Armazenamento S3 local. O MinIO deixou de publicar imagens; RustFS é compatível com a API S3 (ADR 0004).
const string storageAccessKey = "lexflow";
var storageSecretKey = ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, "storage-secret-key", special: false);
var storage = builder.AddContainer("storage", "rustfs/rustfs", "1.0.2")
    .WithEnvironment("RUSTFS_ACCESS_KEY", storageAccessKey)
    .WithEnvironment("RUSTFS_SECRET_KEY", storageSecretKey)
    .WithHttpEndpoint(targetPort: 9000, name: "s3")
    .WithHttpEndpoint(targetPort: 9001, name: "console")
    .WithHttpHealthCheck("/health", endpointName: "s3");
if (useDataVolumes)
{
    storage.WithVolume("lexflow-storage-data", "/data");
}

builder.AddContainer("gotenberg", "gotenberg/gotenberg", "8.38.1")
    .WithHttpEndpoint(targetPort: 3000, name: "http")
    .WithHttpHealthCheck("/health");

var clamav = builder.AddContainer("clamav", "clamav/clamav", "1.5.4-debian")
    .WithEndpoint(targetPort: 3310, name: "clamd", scheme: "tcp")
    .WithClamAvHealthCheck();

// Porta fixa: o e2e lê os e-mails de confirmação pela API do Mailpit em http://localhost:8025.
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.31.6")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "http", isProxied: false)
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithHttpHealthCheck("/livez");

var storageEndpoint = storage.GetEndpoint("s3");
var smtp = mailpit.GetEndpoint("smtp");
var clamd = clamav.GetEndpoint("clamd");

// Migrations aplicadas por um processo próprio, nunca no startup da API (seção 6.5).
var migrations = builder.AddProject<Projects.LexFlow_MigrationService>("migrations")
    .WithReference(database).WaitFor(database)
    .WithEnvironment("Database__ApplicationLogin__Username", appLogin)
    .WithEnvironment("Database__ApplicationLogin__Password", appPassword)
    .WithEnvironment("Storage__ServiceUrl", storageEndpoint)
    .WithEnvironment("Storage__AccessKey", storageAccessKey)
    .WithEnvironment("Storage__SecretKey", storageSecretKey)
    .WaitFor(storage);

var api = builder.AddProject<Projects.LexFlow_Api>("api")
    .WithEnvironment("ConnectionStrings__lexflow", appConnection).WaitFor(database).WaitForCompletion(migrations)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithEnvironment("Storage__ServiceUrl", storageEndpoint)
    .WithEnvironment("Storage__AccessKey", storageAccessKey)
    .WithEnvironment("Storage__SecretKey", storageSecretKey)
    .WaitFor(storage)
    .WithEnvironment("Email__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Email__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("ClamAv__Host", clamd.Property(EndpointProperty.Host))
    .WithEnvironment("ClamAv__Port", clamd.Property(EndpointProperty.Port))
    .WithHttpHealthCheck("/health/ready", endpointName: "http")
    // Local e e2e: o fluxo completo (cadastro, confirmação, dois logins com segundo fator) passa de 10 req/min por IP.
    .WithEnvironment("Identity__PublicPermitsPerMinute", builder.Configuration["LexFlow:PublicPermitsPerMinute"] ?? "120");

// O Worker monta os links dos e-mails de conta com o endereço público da API (servidor de identidade).
builder.AddProject<Projects.LexFlow_Worker>("worker")
    .WithEnvironment("Identity__PublicUrl", api.GetEndpoint("http"))
    // Índices monetários: fonte falsa no ambiente local (decisão de 2026-10-18); o seed traz os valores reais até 09/2026.
    .WithEnvironment("Platform__MonetaryIndexes__Source", "Fake")
    .WithEnvironment("ConnectionStrings__lexflow", appConnection).WaitFor(database).WaitForCompletion(migrations)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithEnvironment("Storage__ServiceUrl", storageEndpoint)
    .WithEnvironment("Storage__AccessKey", storageAccessKey)
    .WithEnvironment("Storage__SecretKey", storageSecretKey)
    .WaitFor(storage)
    .WithEnvironment("Email__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Email__Port", smtp.Property(EndpointProperty.Port))
    .WithEnvironment("ClamAv__Host", clamd.Property(EndpointProperty.Host))
    .WithEnvironment("ClamAv__Port", clamd.Property(EndpointProperty.Port));

// Porta fixa (sem proxy): o `ng serve` escuta em 4200 e o Playwright do CI usa http://localhost:4200.
builder.AddJavaScriptApp("web", "../../Web/lexflow-web", "start")
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithHttpHealthCheck("/")
    .WithReference(api)
    .WaitFor(api);

await builder.Build().RunAsync();

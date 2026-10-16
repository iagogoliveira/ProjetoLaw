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

builder.AddContainer("clamav", "clamav/clamav", "1.5.4-debian")
    .WithEndpoint(targetPort: 3310, name: "clamd", scheme: "tcp")
    .WithClamAvHealthCheck();

builder.AddContainer("mailpit", "axllent/mailpit", "v1.31.6")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithHttpHealthCheck("/livez");

var storageEndpoint = storage.GetEndpoint("s3");

// Migrations aplicadas por um processo próprio, nunca no startup da API (seção 6.5).
var migrations = builder.AddProject<Projects.LexFlow_MigrationService>("migrations")
    .WithReference(database).WaitFor(database);

var api = builder.AddProject<Projects.LexFlow_Api>("api")
    .WithReference(database).WaitForCompletion(migrations)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithEnvironment("Storage__ServiceUrl", storageEndpoint)
    .WithEnvironment("Storage__AccessKey", storageAccessKey)
    .WithEnvironment("Storage__SecretKey", storageSecretKey)
    .WaitFor(storage)
    .WithHttpHealthCheck("/health/ready", endpointName: "http");

builder.AddProject<Projects.LexFlow_Worker>("worker")
    .WithReference(database).WaitForCompletion(migrations)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithEnvironment("Storage__ServiceUrl", storageEndpoint)
    .WithEnvironment("Storage__AccessKey", storageAccessKey)
    .WithEnvironment("Storage__SecretKey", storageSecretKey)
    .WaitFor(storage);

// Porta fixa (sem proxy): o `ng serve` escuta em 4200 e o Playwright do CI usa http://localhost:4200.
builder.AddJavaScriptApp("web", "../../Web/lexflow-web", "start")
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithHttpHealthCheck("/")
    .WithReference(api)
    .WaitFor(api);

await builder.Build().RunAsync();

using LexFlow.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Dependências de dados (seção 8.1). Nomes de recurso = nomes das connection strings.
var sql = builder.AddSqlServer("sql")
    .WithImageTag("2025-latest")
    .WithDataVolume()
    .AddDatabase("lexflow");

var redis = builder.AddRedis("redis");

var rabbit = builder.AddRabbitMQ("rabbit")
    .WithManagementPlugin();

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("blobs");

// Serviços auxiliares em container (usados a partir das Etapas 9 e 11).
builder.AddContainer("opensearch", "opensearchproject/opensearch", "3.9.0")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
    .WithEnvironment("DISABLE_INSTALL_DEMO_CONFIG", "true")
    .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
    .WithHttpEndpoint(port: 9200, targetPort: 9200)
    .WithHttpHealthCheck("/_cluster/health");

builder.AddContainer("gotenberg", "gotenberg/gotenberg", "8.38")
    .WithHttpEndpoint(port: 3000, targetPort: 3000)
    .WithHttpHealthCheck("/health");

builder.AddContainer("clamav", "clamav/clamav", "1.5")
    .WithEndpoint(port: 3310, targetPort: 3310, name: "tcp")
    .WithClamAvHealthCheck();

builder.AddContainer("mailpit", "axllent/mailpit", "v1.31")
    .WithHttpEndpoint(port: 8025, targetPort: 8025)
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp")
    .WithHttpHealthCheck("/livez");

// Processos .NET.
var api = builder.AddProject<Projects.LexFlow_Api>("api")
    .WithReference(sql).WaitFor(sql)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(blobs).WaitFor(blobs)
    .WithHttpHealthCheck("/health/ready");

builder.AddProject<Projects.LexFlow_Worker>("worker")
    .WithReference(sql).WaitFor(sql)
    .WithReference(redis).WaitFor(redis)
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(blobs).WaitFor(blobs);

// Frontend Angular (ng serve na porta fixa 4200, usada pelo e2e).
builder.AddJavaScriptApp("web", "../../Web/lexflow-web", "start")
    .WithReference(api).WaitFor(api)
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithHttpHealthCheck("/")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();

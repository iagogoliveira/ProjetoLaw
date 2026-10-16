using LexFlow.ServiceDefaults;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Mesmas dependências da API; consumidores Wolverine e servidor Hangfire entram na Etapa 1.
builder.AddSqlServerClient("lexflow");
builder.AddRedisClient("redis");
builder.AddRabbitMQClient("rabbit");
builder.AddAzureBlobServiceClient("blobs");

using var host = builder.Build();
await host.RunAsync();

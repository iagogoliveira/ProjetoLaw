using LexFlow.Api;
using LexFlow.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Integrações cliente do Aspire: registram conexão, health check e telemetria de cada dependência.
builder.AddSqlServerClient(ResourceNames.Database);
builder.AddRedisClient(ResourceNames.Redis);
builder.AddRabbitMQClient(ResourceNames.RabbitMq);
builder.AddAzureBlobServiceClient(ResourceNames.Blobs);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

await app.RunAsync();

using LexFlow.Infrastructure.Common.HealthChecks;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);

// Consumidores Wolverine e servidor Hangfire entram na Etapa 1.

using var host = builder.Build();
await host.RunAsync();

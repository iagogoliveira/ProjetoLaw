using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Platform.Application;
using LexFlow.Platform.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);
builder.Services.AddApplication(PlatformApplication.Assembly);
builder.Services.AddSystemContexts();
builder.Services.AddPlatformInfrastructure(builder.Configuration);

// Consumidores Wolverine e servidor Hangfire entram na Etapa 1.

using var host = builder.Build();
await host.RunAsync();

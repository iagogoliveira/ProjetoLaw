using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Platform.Application;
using LexFlow.Platform.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);
builder.Services.AddApplication(PlatformApplication.Assembly);
builder.Services.AddSystemContexts();
builder.Services.AddPlatformInfrastructure(builder.Configuration);

// Consumidores de eventos de integração: assemblies Infrastructure dos módulos.
builder.Services.AddLexFlowMessaging(builder.Configuration, typeof(PlatformInfrastructure).Assembly);
builder.Services.AddExternalAdapters(builder.Configuration);
builder.Services.AddJobScheduling(builder.Configuration, runServer: true);
builder.Services.AddPlatformRecurringJobs();

using var host = builder.Build();
await host.RunAsync();

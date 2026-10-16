using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.HealthChecks;
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

using var host = builder.Build();
await host.RunAsync();

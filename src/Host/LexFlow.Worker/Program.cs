using LexFlow.Application.Abstractions;
using LexFlow.Identity.Application;
using LexFlow.Identity.Infrastructure;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Organization.Application;
using LexFlow.Organization.Infrastructure;
using LexFlow.Platform.Application;
using LexFlow.Platform.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);
builder.Services.AddApplication(PlatformApplication.Assembly, IdentityApplication.Assembly, OrganizationApplication.Assembly);
builder.Services.AddSystemContexts();
builder.Services.AddPlatformInfrastructure(builder.Configuration);
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddOrganizationInfrastructure(builder.Configuration);

// Consumidores de eventos de integração: assemblies Infrastructure dos módulos.
builder.Services.AddLexFlowMessaging(builder.Configuration, typeof(PlatformInfrastructure).Assembly, typeof(IdentityInfrastructure).Assembly);
builder.Services.AddExternalAdapters(builder.Configuration);
builder.Services.AddJobScheduling(builder.Configuration, runServer: true);
builder.Services.AddPlatformRecurringJobs();
builder.Services.AddIdentityRecurringJobs();

using var host = builder.Build();
await host.RunAsync();

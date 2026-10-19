using LexFlow.Application.Abstractions;
using LexFlow.Crm.Application;
using LexFlow.Crm.Infrastructure;
using LexFlow.Identity.Application;
using LexFlow.Identity.Infrastructure;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Litigation.Application;
using LexFlow.Litigation.Infrastructure;
using LexFlow.Organization.Application;
using LexFlow.Organization.Infrastructure;
using LexFlow.Platform.Application;
using LexFlow.Platform.Infrastructure;
using LexFlow.Publications.Application;
using LexFlow.Publications.Infrastructure;
using LexFlow.Publications.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);
builder.Services.AddApplication(PlatformApplication.Assembly, IdentityApplication.Assembly, OrganizationApplication.Assembly, CrmApplication.Assembly, LitigationApplication.Assembly, PublicationsApplication.Assembly);
builder.Services.AddSystemContexts();
builder.Services.AddPlatformInfrastructure(builder.Configuration);
builder.Services.AddPlatformNotifications();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddOrganizationInfrastructure(builder.Configuration);
builder.Services.AddCrmInfrastructure(builder.Configuration);
builder.Services.AddLitigationInfrastructure(builder.Configuration);
builder.Services.AddPublicationsInfrastructure(builder.Configuration);

// Consumidores de eventos de integração: assemblies Infrastructure dos módulos.
builder.Services.AddLexFlowMessaging(builder.Configuration, typeof(PlatformInfrastructure).Assembly, typeof(IdentityInfrastructure).Assembly, typeof(OrganizationInfrastructure).Assembly, typeof(CrmInfrastructure).Assembly, typeof(LitigationInfrastructure).Assembly, typeof(PublicationsInfrastructure).Assembly);
builder.Services.AddExternalAdapters(builder.Configuration);
builder.Services.AddJobScheduling(builder.Configuration, runServer: true);
builder.Services.AddPlatformRecurringJobs();
builder.Services.AddIdentityRecurringJobs();
builder.Services.AddPublicationsRecurringJobs();

using var host = builder.Build();
await host.RunAsync();

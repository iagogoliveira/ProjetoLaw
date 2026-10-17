using LexFlow.Application.Abstractions;
using LexFlow.Crm.Infrastructure;
using LexFlow.Crm.Infrastructure.Persistence;
using LexFlow.Identity.Infrastructure;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.MigrationService;
using LexFlow.Organization.Infrastructure;
using LexFlow.Organization.Infrastructure.Persistence;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.Persistence;

// Provisiona o ambiente e encerra: aplica as migrations de todos os módulos (seção 6.5) e cria o bucket do
// armazenamento de objetos, se não existir. No ambiente local roda pelo AppHost, antes da
// API e do Worker; em produção o equivalente é o bundle do EF Core no pipeline.
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddSystemContexts();
builder.Services.AddPlatformInfrastructure(builder.Configuration);
builder.Services.AddIdentityPersistence(builder.Configuration);
builder.Services.AddIdentitySeeding(builder.Configuration);
builder.Services.AddOrganizationPersistence(builder.Configuration);
builder.Services.AddCrmPersistence(builder.Configuration);
builder.Services.AddExternalAdapters(builder.Configuration);

// Ordem de aplicação: Platform primeiro (extensões e trilha de auditoria usadas pelos demais módulos).
builder.Services.AddSingleton(new MigrationPlan([typeof(PlatformDbContext), typeof(IdentityModuleDbContext), typeof(OrganizationDbContext), typeof(CrmDbContext)]));
builder.Services.AddHostedService<MigrationRunner>();

using var host = builder.Build();
await host.RunAsync();

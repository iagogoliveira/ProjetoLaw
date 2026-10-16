using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.MigrationService;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.Persistence;

// Aplica as migrations de todos os módulos e encerra (seção 6.5). No ambiente local roda pelo AppHost, antes da
// API e do Worker; em produção o equivalente é o bundle do EF Core no pipeline.
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddApplication();
builder.Services.AddSystemContexts();
builder.Services.AddPlatformInfrastructure(builder.Configuration);

// Ordem de aplicação: Platform primeiro (extensões e trilha de auditoria usadas pelos demais módulos).
builder.Services.AddSingleton(new MigrationPlan([typeof(PlatformDbContext)]));
builder.Services.AddHostedService<MigrationRunner>();

using var host = builder.Build();
await host.RunAsync();

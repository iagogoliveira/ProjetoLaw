using LexFlow.Application.Abstractions;
using LexFlow.Crm.Application;
using LexFlow.Crm.Endpoints;
using LexFlow.Crm.Infrastructure;
using LexFlow.Identity.Application;
using LexFlow.Identity.Endpoints;
using LexFlow.Identity.Infrastructure;
using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Litigation.Application;
using LexFlow.Litigation.Endpoints;
using LexFlow.Litigation.Infrastructure;
using LexFlow.Organization.Application;
using LexFlow.Organization.Endpoints;
using LexFlow.Organization.Infrastructure;
using LexFlow.Platform.Application;
using LexFlow.Platform.Endpoints;
using LexFlow.Platform.Infrastructure;
using LexFlow.Publications.Application;
using LexFlow.Publications.Endpoints;
using LexFlow.Publications.Infrastructure;
using LexFlow.Publications.Infrastructure.Persistence;
using LexFlow.Web.Common;
using LexFlow.Web.Common.Modules;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Módulos da API (seção 4.2): cada um registra serviços e mapeia os próprios endpoints.
IModule[] modules = [new PlatformModule(), new IdentityModule(), new OrganizationModule(), new CrmModule(), new LitigationModule(), new PublicationsModule()];

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddWebCommon();

// Módulos: Application (handlers e validadores) + Infrastructure (DbContext, adaptadores).
builder.Services.AddApplication(PlatformApplication.Assembly, IdentityApplication.Assembly, OrganizationApplication.Assembly, CrmApplication.Assembly, LitigationApplication.Assembly, PublicationsApplication.Assembly);
builder.Services.AddPlatformInfrastructure(builder.Configuration);
builder.Services.AddPlatformNotifications();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddIdentityServer(builder.Environment);
builder.Services.AddOrganizationInfrastructure(builder.Configuration);
builder.Services.AddCrmInfrastructure(builder.Configuration);

// Depois do Crm: as partes dos processos substituem o stub do conflito de interesses.
builder.Services.AddLitigationInfrastructure(builder.Configuration);
builder.Services.AddPublicationsInfrastructure(builder.Configuration);

// A API só publica eventos (outbox) e agenda jobs; consumidores e servidor de jobs rodam no Worker.
builder.Services.AddLexFlowMessaging(builder.Configuration);
builder.Services.AddExternalAdapters(builder.Configuration);
builder.Services.AddJobScheduling(builder.Configuration, runServer: false);

foreach (var module in modules)
{
    module.AddServices(builder.Services, builder.Configuration);
}

var app = builder.Build();

app.UseCors();
app.UseWebCommon();
app.UseRateLimiter();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

foreach (var module in modules)
{
    module.MapEndpoints(app);
}

await app.RunAsync();

/// <summary>Exposto para <c>WebApplicationFactory</c> nos testes de integração.</summary>
public partial class Program;

using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Platform.Application;
using LexFlow.Platform.Infrastructure;
using LexFlow.Web.Common;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddWebCommon();

// Módulos: Application (handlers e validadores) + Infrastructure (DbContext, adaptadores).
builder.Services.AddApplication(PlatformApplication.Assembly);
builder.Services.AddPlatformInfrastructure(builder.Configuration);

// A API só publica eventos (outbox) e agenda jobs; consumidores e servidor de jobs rodam no Worker.
builder.Services.AddLexFlowMessaging(builder.Configuration);
builder.Services.AddExternalAdapters(builder.Configuration);
builder.Services.AddJobScheduling(builder.Configuration, runServer: false);

var app = builder.Build();

app.UseWebCommon();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

await app.RunAsync();

/// <summary>Exposto para <c>WebApplicationFactory</c> nos testes de integração.</summary>
public partial class Program;

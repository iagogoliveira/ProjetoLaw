using LexFlow.Infrastructure.Common.HealthChecks;
using LexFlow.Web.Common;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddWebCommon();

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

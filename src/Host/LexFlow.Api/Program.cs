using LexFlow.Infrastructure.Common.HealthChecks;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHealthChecks().AddDependencyHealthChecks(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

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

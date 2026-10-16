var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Consumidores Wolverine e servidor Hangfire entram na Etapa 1.

using var host = builder.Build();
await host.RunAsync();

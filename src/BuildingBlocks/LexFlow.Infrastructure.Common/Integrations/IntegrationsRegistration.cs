using Hangfire;
using Hangfire.PostgreSql;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Infrastructure.Common.Integrations;

public static class IntegrationsRegistration
{
    public const string HangfireSchema = "hangfire";

    /// <summary>Armazenamento S3, e-mail SMTP e antivírus ClamAV, configurados pelas seções <c>Storage</c>, <c>Email</c> e <c>ClamAv</c>.</summary>
    public static IServiceCollection AddExternalAdapters(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(Bind<ObjectStorageSettings>(configuration, ObjectStorageSettings.SectionName));
        services.AddSingleton(Bind<EmailSettings>(configuration, EmailSettings.SectionName));
        services.AddSingleton(Bind<ClamAvSettings>(configuration, ClamAvSettings.SectionName));

        services.AddSingleton<S3ObjectStorage>();
        services.AddSingleton<IObjectStorage>(sp => sp.GetRequiredService<S3ObjectStorage>());
        services.AddSingleton<IObjectStorageProvisioner>(sp => sp.GetRequiredService<S3ObjectStorage>());
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IFileScanner, ClamAvFileScanner>();
        return services;
    }

    /// <summary>
    /// Hangfire com PostgreSQL (schema <c>hangfire</c>). <paramref name="runServer"/> = true só no Worker; a API apenas agenda.
    /// </summary>
    public static IServiceCollection AddJobScheduling(this IServiceCollection services, IConfiguration configuration, bool runServer)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var connectionString = configuration.GetConnectionString(DependencyHealthChecks.DatabaseConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{DependencyHealthChecks.DatabaseConnectionName}' não configurada.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseFilter(new JobContextFilter())
            .UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions { SchemaName = HangfireSchema, PrepareSchemaIfNecessary = true }));

        if (runServer)
        {
            services.AddHangfireServer(options => options.ServerName = $"lexflow-worker-{Environment.MachineName}");
        }

        services.AddScoped<IJobScheduler, HangfireJobScheduler>();
        return services;
    }

    private static T Bind<T>(IConfiguration configuration, string section)
        where T : new() =>
        configuration.GetSection(section).Get<T>() ?? new T();
}

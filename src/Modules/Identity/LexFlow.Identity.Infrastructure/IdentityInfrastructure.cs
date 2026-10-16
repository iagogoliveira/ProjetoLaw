using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Emails;
using LexFlow.Identity.Infrastructure.Passwords;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LexFlow.Identity.Infrastructure;

public static class IdentityInfrastructure
{
    /// <summary>Nome comum do Data Protection: API e Worker precisam do mesmo para ler os tokens um do outro.</summary>
    public const string DataProtectionApplicationName = "LexFlow";

    /// <summary>Só o DbContext do módulo (usado pelo MigrationService).</summary>
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityModuleDbContext>(configuration, IdentityModuleDbContext.ModuleName, IdentityModuleDbContext.SchemaName);
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityModuleDbContext>());
        return services;
    }

    /// <summary>DbContext, ASP.NET Core Identity, Data Protection com chaves no banco e e-mails de conta.</summary>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var settings = configuration.GetSection(IdentitySettings.SectionName).Get<IdentitySettings>() ?? new IdentitySettings();
        services.AddSingleton(settings);
        services.AddIdentityPersistence(configuration);

        services.AddDbContext<DataProtectionKeysDbContext>((sp, options) => options
            .UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>())
            .UseSnakeCaseNamingConvention());
        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<DataProtectionKeysDbContext>();

        var identity = services.AddIdentityCore<ApplicationUser>(options =>
            {
                // Seção 7.1: mínimo de 12 caracteres, sem regras de composição (orientação NIST 800-63B).
                options.Password.RequiredLength = Application.Accounts.PasswordPolicy.MinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                // Critério de aceite: a 6ª tentativa errada bloqueia por 15 minutos.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddDefaultTokenProviders();

        // Links de confirmação e de redefinição valem 24 h (o texto dos e-mails diz isso).
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));

        if (settings.CheckBreachedPasswords)
        {
            services.AddHttpClient<PwnedPasswordValidator>(client =>
            {
                client.BaseAddress = PwnedPasswordValidator.BaseAddress;
                client.Timeout = TimeSpan.FromSeconds(5);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LexFlow");
            });
            identity.Services.AddScoped<IPasswordValidator<ApplicationUser>>(sp => sp.GetRequiredService<PwnedPasswordValidator>());
        }

        services.AddSingleton<AccountEmailComposer>();
        return services;
    }
}

using System.Security.Cryptography.X509Certificates;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Domain.Sessions;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Emails;
using LexFlow.Identity.Infrastructure.Oidc;
using LexFlow.Identity.Infrastructure.Passwords;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

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

    /// <summary>Cadastro do cliente OIDC <c>lexflow-web</c> pelo MigrationService.</summary>
    public static IServiceCollection AddIdentitySeeding(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(BindSettings(configuration));
        services.AddOpenIddict().AddOidcCore();
        services.AddScoped<IDatabaseSeeder, WebClientSeeder>();
        return services;
    }

    /// <summary>DbContext, ASP.NET Core Identity, Data Protection com chaves no banco, stores do OpenIddict e e-mails de conta.</summary>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = BindSettings(configuration);
        services.TryAddSingleton(settings);
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

                // Claims com os nomes do OIDC, lidas pelo endpoint de autorização.
                options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
                options.ClaimsIdentity.UserNameClaimType = Claims.Name;
                options.ClaimsIdentity.EmailClaimType = Claims.Email;
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

        services.AddOpenIddict().AddOidcCore();
        services.AddScoped<ISessionTokens, OpenIddictSessionTokens>();
        services.AddScoped<OidcTokensCleanupJob>();
        services.AddSingleton<AccountEmailComposer>();
        return services;
    }

    /// <summary>
    /// Só na API: servidor OIDC (Authorization Code + PKCE, refresh rotativo), validação local dos access tokens como
    /// esquema padrão (substitui o provisório da ADR 0008), login por cookie de curta duração e <c>SignInManager</c>.
    /// </summary>
    public static IServiceCollection AddIdentityServer(this IServiceCollection services, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        new IdentityBuilder(typeof(ApplicationUser), services).AddSignInManager();
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            })
            .AddIdentityCookies();

        ConfigureLoginCookie(services, environment);

        services.AddOpenIddict()
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetEndSessionEndpointUris("connect/logout");

                options.AllowAuthorizationCodeFlow()
                    .RequireProofKeyForCodeExchange()
                    .AllowRefreshTokenFlow();

                options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, OidcScopes.Api);

                // Seção 5.3: access token de 10 min. Refresh rotativo: cada uso emite outro e invalida o anterior; reuso
                // fora da janela revoga todos os tokens da autorização. Sem tolerância: reuso é sempre tratado como roubo.
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(10))
                    .SetIdentityTokenLifetime(TimeSpan.FromMinutes(10))
                    .SetRefreshTokenLifetime(Session.IdleTimeout)
                    .SetRefreshTokenReuseLeeway(TimeSpan.Zero);

                AddKeys(options, services, environment);

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
                if (environment.IsDevelopment())
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }

    /// <summary>Job recorrente do módulo (registrado pelo Worker na inicialização).</summary>
    public static IServiceCollection AddIdentityRecurringJobs(this IServiceCollection services) =>
        services.AddHostedService<IdentityRecurringJobsRegistration>();

    /// <summary>O cookie só vive entre a senha e a emissão do código: o endpoint de autorização o apaga (ADR 0014).</summary>
    private static void ConfigureLoginCookie(IServiceCollection services, IHostEnvironment environment) =>
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = Application.Accounts.AccountLinks.LoginPath;
            options.Cookie.Name = "lexflow.login";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
            options.SlidingExpiration = false;
        });

    private static void AddKeys(OpenIddictServerBuilder options, IServiceCollection services, IHostEnvironment environment)
    {
        var settings = (IdentitySettings?)services.FirstOrDefault(d => d.ServiceType == typeof(IdentitySettings))?.ImplementationInstance
            ?? throw new InvalidOperationException("Registre AddIdentityInfrastructure antes de AddIdentityServer.");
        var server = settings.Server;

        if (server is { SigningCertificate: { Length: > 0 } signing, EncryptionCertificate: { Length: > 0 } encryption })
        {
            options.AddSigningCertificate(LoadCertificate(signing, server.CertificatePassword))
                .AddEncryptionCertificate(LoadCertificate(encryption, server.CertificatePassword));
        }
        else if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Configure Identity:Server:SigningCertificate e EncryptionCertificate (cofre de segredos).");
        }
        else if (server.UseEphemeralKeys)
        {
            options.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
        }
        else
        {
            options.AddDevelopmentSigningCertificate().AddDevelopmentEncryptionCertificate();
        }
    }

    private static X509Certificate2 LoadCertificate(string base64Pfx, string? password) =>
        X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(base64Pfx), password, X509KeyStorageFlags.EphemeralKeySet);

    private static IdentitySettings BindSettings(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.GetSection(IdentitySettings.SectionName).Get<IdentitySettings>() ?? new IdentitySettings();
    }

    private sealed class IdentityRecurringJobsRegistration(IServiceScopeFactory scopes) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IJobScheduler>().AddOrUpdateRecurring<OidcTokensCleanupJob>(
                OidcTokensCleanupJob.RecurringJobId,
                job => job.RunAsync(),
                OidcTokensCleanupJob.Cron);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

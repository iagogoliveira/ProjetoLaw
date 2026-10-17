using LexFlow.Application.Abstractions;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Domain.Sessions;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LexFlow.Identity.Infrastructure.Oidc;

/// <summary>Escopo da API do LexFlow, pedido pelo SPA junto com <c>openid profile email offline_access</c>.</summary>
public static class OidcScopes
{
    public const string Api = "api";
}

/// <summary>Tokens da sessão sobre o OpenIddict: revogação (sessão encerrada, senha redefinida) e consulta de refresh válido.</summary>
internal sealed class OpenIddictSessionTokens(IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenManager tokens, IClock clock) : ISessionTokens
{
    public async Task<bool> HasActiveRefreshTokenAsync(string authorizationId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await foreach (var token in tokens.FindByAuthorizationIdAsync(authorizationId, cancellationToken))
        {
            var type = await tokens.GetTypeAsync(token, cancellationToken);
            if ((string.Equals(type, TokenTypeHints.RefreshToken, StringComparison.Ordinal) || string.Equals(type, TokenTypeIdentifiers.RefreshToken, StringComparison.Ordinal))
                && await tokens.HasStatusAsync(token, Statuses.Valid, cancellationToken)
                && (await tokens.GetExpirationDateAsync(token, cancellationToken) ?? DateTimeOffset.MaxValue) > now)
            {
                return true;
            }
        }

        return false;
    }

    public async Task RevokeAsync(string authorizationId, CancellationToken cancellationToken)
    {
        if (await authorizations.FindByIdAsync(authorizationId, cancellationToken) is { } authorization)
        {
            await authorizations.TryRevokeAsync(authorization, cancellationToken);
        }

        await tokens.RevokeByAuthorizationIdAsync(authorizationId, cancellationToken);
    }
}

/// <summary>
/// Cadastra ou atualiza o cliente <c>lexflow-web</c> (rodado pelo MigrationService depois das migrations): público,
/// PKCE obrigatório, consentimento implícito (aplicação da própria plataforma) e endereços da configuração.
/// </summary>
internal sealed class WebClientSeeder(IOpenIddictApplicationManager applications, IdentitySettings settings) : IDatabaseSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = WebClientSettings.ClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "LexFlow Web",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Email,
                Permissions.Prefixes.Scope + OidcScopes.Api,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (var uri in settings.WebClient.EffectiveRedirectUris)
        {
            descriptor.RedirectUris.Add(uri);
        }

        foreach (var uri in settings.WebClient.EffectivePostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(uri);
        }

        if (await applications.FindByClientIdAsync(WebClientSettings.ClientId, cancellationToken) is { } existing)
        {
            await applications.UpdateAsync(existing, descriptor, cancellationToken);
        }
        else
        {
            await applications.CreateAsync(descriptor, cancellationToken);
        }
    }
}

/// <summary>Remove tokens e autorizações inválidos ou expirados há mais tempo que a idade máxima da sessão.</summary>
public sealed class OidcTokensCleanupJob(IOpenIddictTokenManager tokens, IOpenIddictAuthorizationManager authorizations, IClock clock)
{
    public const string RecurringJobId = "identity-oidc-tokens-cleanup";

    /// <summary>Todo dia às 03:30 (America/Sao_Paulo).</summary>
    public const string Cron = "30 3 * * *";

    public async Task RunAsync()
    {
        var threshold = clock.UtcNow - Session.AbsoluteLifetime;
        await tokens.PruneAsync(threshold);
        await authorizations.PruneAsync(threshold);
    }
}

internal static class OidcStores
{
    /// <summary>Gerenciadores e stores do OpenIddict sobre o <see cref="IdentityModuleDbContext"/>, com chave Guid.</summary>
    public static OpenIddictBuilder AddOidcCore(this OpenIddictBuilder builder) =>
        builder.AddCore(options => options
            .UseEntityFrameworkCore()
            .UseDbContext<IdentityModuleDbContext>()
            .ReplaceDefaultEntities<Guid>());
}

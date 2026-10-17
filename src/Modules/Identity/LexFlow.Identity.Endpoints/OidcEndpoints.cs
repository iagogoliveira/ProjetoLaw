using System.Security.Claims;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Sessions;
using LexFlow.Identity.Domain.Users;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LexFlow.Identity.Endpoints;

/// <summary>
/// Endpoints do servidor OIDC (seção 7.1) em modo passthrough: o OpenIddict valida o protocolo (cliente, PKCE, códigos,
/// rotação de refresh token) e estes handlers decidem quem entra e ligam cada login a uma sessão (<c>sid</c>).
/// Ficam fora do OpenAPI: são protocolo, não API do LexFlow.
/// </summary>
internal static class OidcEndpoints
{
    public const string SessionClaim = LexFlow.Web.Common.Security.LexFlowClaims.SessionId;

    private const string LoginScheme = "Identity.Application";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync).ExcludeFromDescription();
        endpoints.MapPost("/connect/token", TokenAsync).ExcludeFromDescription();
        endpoints.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], LogoutAsync).ExcludeFromDescription();
    }

    /// <summary>
    /// Sem login, manda para a página de login e volta. Com login, abre a autorização e a sessão, emite o código e apaga
    /// o cookie de login: a partir daqui a sessão vive nos tokens, e um novo authorize pede a senha de novo.
    /// </summary>
    private static async Task<IResult> AuthorizeAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations,
        ICommandHandler<StartSessionCommand, Guid> startSession,
        IRequestContext origin)
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Requisição OIDC ausente.");
        var login = await context.AuthenticateAsync(LoginScheme);
        var user = login.Succeeded ? await users.GetUserAsync(login.Principal) : null;

        if (user is null || request.HasPromptValue(PromptValues.Login))
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Reject(Errors.LoginRequired, "É preciso entrar.");
            }

            var parameters = context.Request.HasFormContentType ? [.. context.Request.Form] : context.Request.Query.ToList();
            var returnUrl = context.Request.PathBase + context.Request.Path + QueryString.Create(
                parameters.Where(p => !string.Equals(p.Key, Parameters.Prompt, StringComparison.Ordinal)).Select(p => KeyValuePair.Create(p.Key, p.Value)));
            return Results.Challenge(new AuthenticationProperties { RedirectUri = returnUrl }, [LoginScheme]);
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("Cliente validado pelo OpenIddict não encontrado.");

        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.FullName)
            .SetClaims(Claims.AuthenticationMethodReference, login.Principal!.HasClaim(Claims.AuthenticationMethodReference, "mfa") ? ["pwd", "mfa"] : ["pwd"]);
        identity.SetScopes(request.GetScopes());

        var authorization = await authorizations.CreateAsync(
            identity, user.Id.ToString(), (await applications.GetIdAsync(application))!, AuthorizationTypes.Permanent, identity.GetScopes());
        var authorizationId = (await authorizations.GetIdAsync(authorization))!;
        identity.SetAuthorizationId(authorizationId);

        var session = await startSession.Handle(new StartSessionCommand(user.Id, authorizationId, origin.IpAddress, origin.UserAgent), context.RequestAborted);
        if (session.IsFailure)
        {
            return Reject(Errors.AccessDenied, session.Error.Message);
        }

        identity.SetClaim(SessionClaim, session.Value.ToString());
        identity.SetDestinations(ClaimDestinations);

        await context.SignOutAsync(LoginScheme);
        return Results.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Troca de código e renovação: só seguem se a sessão continuar ativa; cada uso renova a janela de 14 dias.</summary>
    private static async Task<IResult> TokenAsync(HttpContext context, ICommandHandler<RenewSessionCommand, Unit> renewSession)
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Requisição OIDC ausente.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return Reject(Errors.UnsupportedGrantType, "Tipo de concessão não suportado.");
        }

        var principal = (await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;
        if (principal is null
            || !Guid.TryParse(principal.GetClaim(SessionClaim), out var sessionId)
            || !Guid.TryParse(principal.GetClaim(Claims.Subject), out var userId))
        {
            return Reject(Errors.InvalidGrant, "Token inválido.");
        }

        var renewed = await renewSession.Handle(new RenewSessionCommand(sessionId, userId), context.RequestAborted);
        if (renewed.IsFailure)
        {
            return Reject(Errors.InvalidGrant, renewed.Error.Message);
        }

        principal.SetDestinations(ClaimDestinations);
        return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>Encerra a sessão indicada pelo <c>id_token_hint</c> (e revoga os tokens dela) e volta para o SPA.</summary>
    private static async Task<IResult> LogoutAsync(HttpContext context, ICommandHandler<EndSessionCommand, Unit> endSession)
    {
        var hint = (await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal;
        if (Guid.TryParse(hint?.GetClaim(SessionClaim), out var sessionId))
        {
            await endSession.Handle(new EndSessionCommand(sessionId, SessionEndReasons.Logout), context.RequestAborted);
        }

        await context.SignOutAsync(LoginScheme);
        return Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static IResult Reject(string error, string description) =>
        Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    /// <summary>Nome e e-mail vão ao id token só com os escopos <c>profile</c> e <c>email</c>; o resto, só ao access token.</summary>
    private static IEnumerable<string> ClaimDestinations(Claim claim)
    {
        var identity = claim.Subject ?? throw new InvalidOperationException("Claim sem identidade.");
        switch (claim.Type)
        {
            case Claims.Subject or SessionClaim or Claims.AuthenticationMethodReference:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                break;
            case Claims.Name:
                yield return Destinations.AccessToken;
                if (identity.HasScope(Scopes.Profile))
                {
                    yield return Destinations.IdentityToken;
                }

                break;
            case Claims.Email:
                yield return Destinations.AccessToken;
                if (identity.HasScope(Scopes.Email))
                {
                    yield return Destinations.IdentityToken;
                }

                break;
            default:
                yield return Destinations.AccessToken;
                break;
        }
    }
}

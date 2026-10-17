using System.Security.Claims;
using System.Text.Encodings.Web;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Esquema de teste até a Etapa 2 (OpenIddict): <c>X-Test-User</c> vira a claim <c>sub</c> e <c>X-Test-Org</c> a claim <c>org_id</c>.
/// </summary>
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string OrganizationHeader = "X-Test-Org";
    public const string SessionHeader = "X-Test-Session";
    public const string AmrHeader = "X-Test-Amr";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers[UserHeader].ToString();
        if (string.IsNullOrEmpty(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(LexFlowClaims.Subject, user) };
        var organization = Request.Headers[OrganizationHeader].ToString();
        if (!string.IsNullOrEmpty(organization))
        {
            claims.Add(new Claim(LexFlowClaims.OrganizationId, organization));
        }

        var session = Request.Headers[SessionHeader].ToString();
        if (!string.IsNullOrEmpty(session))
        {
            claims.Add(new Claim(LexFlowClaims.SessionId, session));
        }

        claims.AddRange(Request.Headers[AmrHeader].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(amr => new Claim(LexFlowClaims.AuthenticationMethods, amr)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

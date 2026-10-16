using System.Security.Claims;
using System.Text.Encodings.Web;
using LexFlow.Web.Common;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LexFlow.BuildingBlocks.UnitTests.Web;

/// <summary>API mínima em memória com o Web.Common, para testar o pipeline HTTP sem o host real.</summary>
internal static class TestWebHost
{
    public const string TestScheme = "Test";
    public const string UserHeader = "X-Test-User";
    public const string OrganizationHeader = "X-Test-Org";

    public static async Task<WebApplication> StartAsync(
        Action<WebApplication> mapEndpoints,
        bool withTestAuthentication = false,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddWebCommon();
        configureServices?.Invoke(builder.Services);
        if (withTestAuthentication)
        {
            builder.Services.AddAuthentication(TestScheme)
                .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(TestScheme, null);
        }

        var app = builder.Build();
        app.UseWebCommon();
        mapEndpoints(app);
        await app.StartAsync();
        return app;
    }

    /// <summary>Autentica quando a requisição traz <see cref="UserHeader"/> (claims <c>sub</c> e <c>org_id</c>).</summary>
    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var user = Request.Headers[UserHeader].ToString();
            if (string.IsNullOrEmpty(user))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(LexFlowClaims.Subject, user) };
            var org = Request.Headers[OrganizationHeader].ToString();
            if (!string.IsNullOrEmpty(org))
            {
                claims.Add(new Claim(LexFlowClaims.OrganizationId, org));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TestScheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, TestScheme)));
        }
    }
}

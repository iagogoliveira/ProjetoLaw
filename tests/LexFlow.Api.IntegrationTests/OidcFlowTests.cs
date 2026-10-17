using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using LexFlow.Application.Abstractions;
using LexFlow.Identity.Application.Accounts;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Servidor OIDC pela API real: login na página Razor, Authorization Code + PKCE do cliente <c>lexflow-web</c>,
/// refresh rotativo, sessões, bloqueio por tentativas e rate limit (Etapa 2, PR B1).
/// </summary>
public sealed partial class OidcFlowTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private const string ClientId = "lexflow-web";
    private const string RedirectUri = "http://localhost:4200/auth/callback";
    private const string Password = "uma senha bem longa";
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _browser = null!;

    public ValueTask InitializeAsync()
    {
        _browser = NewBrowser(_factory);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record Tokens(string AccessToken, string RefreshToken, string IdToken);

    /// <summary>Cliente com cookies e sem seguir redirecionamentos, para inspecionar cada passo do fluxo.</summary>
    private static HttpClient NewBrowser(LexFlowApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    private async Task<ApplicationUser> CreateConfirmedUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = ApplicationUser.Register(Guid.CreateVersion7(), $"{Guid.CreateVersion7():N}@exemplo.com.br", "Ana Souza", new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        user.EmailConfirmed = true;
        (await users.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        return user;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"", RegexOptions.None, 1000)]
    private static partial Regex AntiforgeryToken();

    /// <summary>Envia o formulário de login; devolve a resposta (redirecionamento em sucesso, página com erro em falha).</summary>
    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient browser, string email, string password, string? returnUrl = null)
    {
        var page = await browser.GetStringAsync(new Uri("/conta/entrar", UriKind.Relative), Ct);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = AntiforgeryToken().Match(page).Groups["token"].Value,
        };
        if (returnUrl is not null)
        {
            fields["ReturnUrl"] = returnUrl;
        }

        using var form = new FormUrlEncodedContent(fields);
        return await browser.PostAsync(new Uri("/conta/entrar", UriKind.Relative), form, Ct);
    }

    /// <summary>Authorization Code + PKCE completo, como o SPA faria: authorize → login → authorize → token.</summary>
    private async Task<Tokens> SignInAsync(HttpClient browser, string email)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorize = "/connect/authorize?" + string.Join('&',
            $"client_id={ClientId}",
            "response_type=code",
            $"redirect_uri={Uri.EscapeDataString(RedirectUri)}",
            $"scope={Uri.EscapeDataString("openid profile email offline_access api")}",
            $"code_challenge={challenge}",
            "code_challenge_method=S256",
            "state=estado-1");

        using var toLogin = await browser.GetAsync(new Uri(authorize, UriKind.Relative), Ct);
        toLogin.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var login = Absolute(toLogin.Headers.Location!);
        login.AbsolutePath.ShouldBe("/conta/entrar");
        var returnUrl = HttpUtility.ParseQueryString(login.Query)["ReturnUrl"]!;

        using var loggedIn = await PostLoginAsync(browser, email, Password, returnUrl);
        loggedIn.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        using var toClient = await browser.GetAsync(loggedIn.Headers.Location, Ct);
        toClient.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var callback = toClient.Headers.Location!;
        callback.GetLeftPart(UriPartial.Path).ShouldBe(RedirectUri);
        var query = HttpUtility.ParseQueryString(callback.Query);
        query["state"].ShouldBe("estado-1");

        using var token = await TokenRequestAsync(browser, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = query["code"]!,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = verifier,
        });
        token.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ReadTokensAsync(token);
    }

    private static async Task<HttpResponseMessage> TokenRequestAsync(HttpClient client, Dictionary<string, string> fields)
    {
        using var form = new FormUrlEncodedContent(fields);
        return await client.PostAsync(new Uri("/connect/token", UriKind.Relative), form, Ct);
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        TokenRequestAsync(client, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId,
            ["refresh_token"] = refreshToken,
        });

    private static async Task<Tokens> ReadTokensAsync(HttpResponseMessage response)
    {
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        return new Tokens(body.GetProperty("access_token").GetString()!, body.GetProperty("refresh_token").GetString()!, body.GetProperty("id_token").GetString()!);
    }

    private static async Task<string?> ErrorOfAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("error").GetString();

    private static Uri Absolute(Uri location) => location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location);

    private static string Base64Url(byte[] bytes) => System.Buffers.Text.Base64Url.EncodeToString(bytes);

    private async Task<List<LexFlow.Identity.Domain.Sessions.Session>> SessionsOfAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>().Sessions.AsNoTracking()
            .Where(s => s.UserId == userId).ToListAsync(Ct);
    }

    [Fact]
    public async Task Login_with_pkce_issues_tokens_bound_to_a_new_session()
    {
        var user = await CreateConfirmedUserAsync();

        var tokens = await SignInAsync(_browser, user.Email!);

        var session = (await SessionsOfAsync(user.Id)).Single();
        session.AuthorizationId.ShouldNotBeNullOrEmpty();
        session.RevokedAt.ShouldBeNull();
        await using var scope = _factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id.ToString()))!.LastLoginAt.ShouldNotBeNull();
        tokens.IdToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Access_token_authenticates_api_requests()
    {
        var user = await CreateConfirmedUserAsync();

        // Sem o esquema de teste: só a validação real do OpenIddict, como em produção (chaves no mesmo processo).
        await using var production = new LexFlowApiFactory(dependencies.Settings, testAuthentication: false);
        using var issuer = NewBrowser(production);
        var tokens = await SignInAsync(issuer, user.Email!);
        using var api = production.CreateClient();

        using var anonymous = await api.GetAsync(new Uri($"/api/v1/jobs/{Guid.CreateVersion7()}", UriKind.Relative), Ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{Guid.CreateVersion7()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        using var authenticated = await api.SendAsync(request, Ct);

        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        authenticated.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_cookie_is_dropped_after_the_code_so_a_new_authorization_asks_for_the_password()
    {
        var user = await CreateConfirmedUserAsync();
        await SignInAsync(_browser, user.Email!);

        using var again = await _browser.GetAsync(new Uri($"/connect/authorize?client_id={ClientId}&response_type=code&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope=openid&code_challenge={Base64Url(SHA256.HashData("x"u8.ToArray()))}&code_challenge_method=S256", UriKind.Relative), Ct);

        again.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Absolute(again.Headers.Location!).AbsolutePath.ShouldBe("/conta/entrar");
    }

    /// <summary>Critério de aceite da Etapa 2: reutilizar um refresh token antigo revoga a família inteira.</summary>
    [Fact]
    public async Task Reusing_an_old_refresh_token_revokes_the_whole_family()
    {
        var user = await CreateConfirmedUserAsync();
        var first = await SignInAsync(_browser, user.Email!);

        using var rotated = await RefreshAsync(_browser, first.RefreshToken);
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await ReadTokensAsync(rotated);
        second.RefreshToken.ShouldNotBe(first.RefreshToken);

        using var reuse = await RefreshAsync(_browser, first.RefreshToken);
        using var afterTheft = await RefreshAsync(_browser, second.RefreshToken);

        reuse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(reuse)).ShouldBe("invalid_grant");
        afterTheft.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(afterTheft)).ShouldBe("invalid_grant");
    }

    [Fact]
    public async Task Refresh_renews_the_session_and_is_refused_once_the_session_ends()
    {
        var user = await CreateConfirmedUserAsync();
        var tokens = await SignInAsync(_browser, user.Email!);
        var before = (await SessionsOfAsync(user.Id)).Single();

        using (var renewed = await RefreshAsync(_browser, tokens.RefreshToken))
        {
            renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
            tokens = await ReadTokensAsync(renewed);
        }

        (await SessionsOfAsync(user.Id)).Single().LastSeenAt.ShouldBeGreaterThanOrEqualTo(before.LastSeenAt);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
            var session = await db.Sessions.SingleAsync(s => s.Id == before.Id, Ct);
            session.Revoke(session.LastSeenAt, "teste");
            await db.SaveChangesAsync(Ct);
        }

        using var refused = await RefreshAsync(_browser, tokens.RefreshToken);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorOfAsync(refused)).ShouldBe("invalid_grant");
    }

    [Fact]
    public async Task Logout_ends_the_session_and_revokes_its_tokens()
    {
        var user = await CreateConfirmedUserAsync();
        var tokens = await SignInAsync(_browser, user.Email!);

        using var logout = await _browser.GetAsync(new Uri(
            $"/connect/logout?id_token_hint={tokens.IdToken}&post_logout_redirect_uri={Uri.EscapeDataString("http://localhost:4200/")}", UriKind.Relative), Ct);

        logout.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        logout.Headers.Location!.ToString().ShouldStartWith("http://localhost:4200/");
        (await SessionsOfAsync(user.Id)).Single().RevokedReason.ShouldBe("logout");
        using var refresh = await RefreshAsync(_browser, tokens.RefreshToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Password_reset_ends_every_open_session()
    {
        var user = await CreateConfirmedUserAsync();
        var tokens = await SignInAsync(_browser, user.Email!);
        string resetToken;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            resetToken = await users.GeneratePasswordResetTokenAsync((await users.FindByIdAsync(user.Id.ToString()))!);
        }

        using var api = _factory.CreateClient();
        using var reset = await api.PostAsync(
            new Uri("/api/v1/auth/reset-password", UriKind.Relative),
            JsonContent(new { userId = user.Id, token = AccountTokens.Encode(resetToken), newPassword = "a nova senha bem longa" }),
            Ct);

        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SessionsOfAsync(user.Id)).Single().RevokedReason.ShouldBe("password_reset");
        using var refresh = await RefreshAsync(_browser, tokens.RefreshToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>Critério de aceite da Etapa 2: a 6ª tentativa de senha errada bloqueia a conta por 15 min.</summary>
    [Fact]
    public async Task Sixth_attempt_after_five_wrong_passwords_is_blocked_for_15_minutes()
    {
        var user = await CreateConfirmedUserAsync();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var wrong = await PostLoginAsync(_browser, user.Email!, "senha errada " + attempt);
            (await wrong.Content.ReadAsStringAsync(Ct)).ShouldContain("E-mail ou senha inválidos.");
        }

        using (var fifth = await PostLoginAsync(_browser, user.Email!, "senha errada 5"))
        {
            fifth.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var sixth = await PostLoginAsync(_browser, user.Email!, Password);

        sixth.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await sixth.Content.ReadAsStringAsync(Ct)).ShouldContain("Conta bloqueada por 15 minutos");
        await using var scope = _factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id.ToString());
        var remaining = stored!.LockoutEnd!.Value - _factory.Services.GetRequiredService<IClock>().UtcNow;
        remaining.ShouldBe(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(1));
        (await SessionsOfAsync(user.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Unconfirmed_email_cannot_sign_in()
    {
        var email = $"{Guid.CreateVersion7():N}@exemplo.com.br";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            (await users.CreateAsync(ApplicationUser.Register(Guid.CreateVersion7(), email, "Ana", new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero)), Password)).Succeeded.ShouldBeTrue();
        }

        using var response = await PostLoginAsync(_browser, email, Password);

        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("Confirme seu e-mail antes de entrar.");
    }

    [Fact]
    public async Task Account_pages_are_served_in_portuguese()
    {
        foreach (var (path, title) in new[]
        {
            ("/conta/entrar", "Entrar"),
            ("/conta/cadastro", "Criar conta"),
            ("/conta/esqueci-a-senha", "Esqueci a senha"),
            ("/conta/redefinir-senha?userId=00000000-0000-0000-0000-000000000001&token=x", "Redefinir senha"),
            ("/conta/confirmar-email?userId=00000000-0000-0000-0000-000000000001&token=x", "O link é inválido ou expirou."),
        })
        {
            var html = await _browser.GetStringAsync(new Uri(path, UriKind.Relative), Ct);
            html.ShouldContain("lang=\"pt-BR\"");
            html.ShouldContain(title);
        }
    }

    [Fact]
    public async Task Public_account_endpoints_are_rate_limited_per_ip()
    {
        var settings = dependencies.Settings.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        settings["Identity:PublicPermitsPerMinute"] = "2";
        await using var factory = new LexFlowApiFactory(settings);
        using var browser = NewBrowser(factory);

        using var first = await browser.GetAsync(new Uri("/conta/entrar", UriKind.Relative), Ct);
        using var second = await browser.GetAsync(new Uri("/conta/esqueci-a-senha", UriKind.Relative), Ct);
        using var third = await browser.PostAsync(new Uri("/api/v1/auth/forgot-password", UriKind.Relative), JsonContent(new { email = "a@exemplo.com.br" }), Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    private static StringContent JsonContent(object body) => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using LexFlow.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

internal sealed record OidcTokens(string AccessToken, string RefreshToken, string IdToken);

/// <summary>
/// O papel do SPA e do navegador nos testes: Authorization Code + PKCE do cliente <c>lexflow-web</c>, passando pelas
/// páginas Razor de login e de segundo fator, e as chamadas ao endpoint de token.
/// </summary>
internal static partial class OidcTestClient
{
    public const string ClientId = "lexflow-web";
    public const string RedirectUri = "http://localhost:4200/auth/callback";
    public const string Password = "uma senha bem longa";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Cliente com cookies e sem seguir redirecionamentos, para inspecionar cada passo do fluxo.</summary>
    public static HttpClient NewBrowser(LexFlowApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    public static async Task<ApplicationUser> CreateUserAsync(LexFlowApiFactory factory, bool confirmed = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = ApplicationUser.Register(Guid.CreateVersion7(), $"{Guid.CreateVersion7():N}@exemplo.com.br", "Ana Souza", new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        user.EmailConfirmed = confirmed;
        (await users.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        return user;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"", RegexOptions.None, 1000)]
    private static partial Regex AntiforgeryToken();

    /// <summary>Lê a página (para o token antiforgery) e envia o formulário com os campos dados.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient browser, string pageUrl, Dictionary<string, string> fields, string? handler = null)
    {
        var page = await browser.GetStringAsync(new Uri(pageUrl, UriKind.Relative), Ct);
        fields["__RequestVerificationToken"] = AntiforgeryToken().Match(page).Groups["token"].Value;
        using var form = new FormUrlEncodedContent(fields);
        var path = new Uri(new Uri("http://localhost"), pageUrl).AbsolutePath + (handler is null ? string.Empty : $"?handler={handler}");
        return await browser.PostAsync(new Uri(path, UriKind.Relative), form, Ct);
    }

    /// <summary>Formulário de login; devolve a resposta (redirecionamento em sucesso, página com erro em falha).</summary>
    public static Task<HttpResponseMessage> PostLoginAsync(HttpClient browser, string email, string password, string? returnUrl = null)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["Email"] = email, ["Password"] = password };
        if (returnUrl is not null)
        {
            fields["ReturnUrl"] = returnUrl;
        }

        return PostFormAsync(browser, "/conta/entrar", fields);
    }

    /// <summary>Formulário do segundo fator, na página para onde o login redirecionou.</summary>
    public static Task<HttpResponseMessage> PostSecondFactorAsync(HttpClient browser, Uri secondFactorPage, string code, bool useRecoveryCode = false)
    {
        var page = Absolute(secondFactorPage);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Code"] = code,
            ["UseRecoveryCode"] = useRecoveryCode ? "true" : "false",
            ["ReturnUrl"] = HttpUtility.ParseQueryString(page.Query)["ReturnUrl"] ?? string.Empty,
        };
        return PostFormAsync(browser, page.PathAndQuery, fields);
    }

    /// <summary>
    /// Authorization Code + PKCE completo: authorize → login → (segundo fator) → authorize → token. Com MFA ligado,
    /// <paramref name="secondFactor"/> recebe a página do segundo fator e devolve a resposta do envio do código.
    /// </summary>
    public static async Task<OidcTokens> SignInAsync(HttpClient browser, string email, Func<Uri, Task<HttpResponseMessage>>? secondFactor = null)
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

        var loggedIn = await PostLoginAsync(browser, email, Password, returnUrl);
        loggedIn.StatusCode.ShouldBe(HttpStatusCode.Redirect, await loggedIn.Content.ReadAsStringAsync(Ct));
        if (Absolute(loggedIn.Headers.Location!).AbsolutePath == "/conta/segundo-fator")
        {
            secondFactor.ShouldNotBeNull("O login pediu o segundo fator.");
            var page = loggedIn.Headers.Location!;
            loggedIn.Dispose();
            loggedIn = await secondFactor(page);
            loggedIn.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        }

        using var toClient = await browser.GetAsync(loggedIn.Headers.Location, Ct);
        loggedIn.Dispose();
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

    public static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        TokenRequestAsync(client, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId,
            ["refresh_token"] = refreshToken,
        });

    public static async Task<OidcTokens> ReadTokensAsync(HttpResponseMessage response)
    {
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        return new OidcTokens(body.GetProperty("access_token").GetString()!, body.GetProperty("refresh_token").GetString()!, body.GetProperty("id_token").GetString()!);
    }

    public static async Task<string?> ErrorOfAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("error").GetString();

    /// <summary>Payload de um JWT (id token), sem validar a assinatura.</summary>
    public static JsonElement JwtPayload(string jwt) =>
        JsonDocument.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(jwt.Split('.')[1])).RootElement;

    public static Uri Absolute(Uri location) => location.IsAbsoluteUri ? location : new Uri(new Uri("http://localhost"), location);

    public static string Base64Url(byte[] bytes) => System.Buffers.Text.Base64Url.EncodeToString(bytes);

    private static async Task<HttpResponseMessage> TokenRequestAsync(HttpClient client, Dictionary<string, string> fields)
    {
        using var form = new FormUrlEncodedContent(fields);
        return await client.PostAsync(new Uri("/connect/token", UriKind.Relative), form, Ct);
    }
}

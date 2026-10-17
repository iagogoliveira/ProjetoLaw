using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Emails;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>MFA (TOTP, código por e-mail, recuperação) e "minha conta" pela API real (Etapa 2, PR B2).</summary>
public sealed partial class MfaAndProfileTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _browser = null!;
    private HttpClient _api = null!;

    private sealed class FakeEmailSender : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Sent { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    public ValueTask InitializeAsync()
    {
        _browser = OidcTestClient.NewBrowser(_factory);
        _api = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        _api.Dispose();
        await _factory.DisposeAsync();
    }

    private DateTimeOffset Now => _factory.Services.GetRequiredService<IClock>().UtcNow;

    /// <summary>Requisição autenticada pelo esquema de teste como o usuário (e, opcionalmente, a sessão) dados.</summary>
    private static HttpRequestMessage As(Guid userId, HttpMethod method, string path, object? body = null, Guid? session = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, userId.ToString());
        if (session is { } sid)
        {
            request.Headers.Add(TestAuthenticationHandler.SessionHeader, sid.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, HttpStatusCode expected)
    {
        using (request)
        using (var response = await _api.SendAsync(request, Ct))
        {
            var text = await response.Content.ReadAsStringAsync(Ct);
            response.StatusCode.ShouldBe(expected, text);
            return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        }
    }

    private async Task<(string Key, string[] RecoveryCodes)> EnableTotpAsync(Guid userId)
    {
        var setup = await SendAsync(As(userId, HttpMethod.Post, "/api/v1/me/mfa/totp"), HttpStatusCode.OK);
        var key = setup.GetProperty("sharedKey").GetString()!;
        var confirmed = await SendAsync(As(userId, HttpMethod.Post, "/api/v1/me/mfa/totp/confirm", new { code = Totp.Code(key, Now) }), HttpStatusCode.OK);
        return (key, [.. confirmed.GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!)]);
    }

    [GeneratedRegex(@"letter-spacing:6px;"">(?<code>\d{6})<", RegexOptions.None, 1000)]
    private static partial Regex SignInCode();

    /// <summary>Faz o papel do Worker para o pedido de código de acesso e devolve o código do e-mail.</summary>
    private async Task<string> DeliverSignInCodeAsync(Guid userId)
    {
        var request = _factory.PublishedEvents.OfType<AccountEmailRequestedIntegrationEvent>()
            .Last(e => e.UserId == userId && e.Kind == AccountEmailKind.SignInCode);
        var email = new FakeEmailSender();
        await using (var worker = dependencies.WorkerServices(email))
        await using (var scope = worker.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AccountEmailConsumer>().Handle(request with { EventId = Guid.CreateVersion7() }, Ct);
        }

        return SignInCode().Match(email.Sent.Single().HtmlBody).Groups["code"].Value;
    }

    [Fact]
    public async Task Profile_is_returned_for_the_signed_in_user_only()
    {
        var user = await OidcTestClient.CreateUserAsync(_factory);

        var me = await SendAsync(As(user.Id, HttpMethod.Get, "/api/v1/me"), HttpStatusCode.OK);
        using var anonymous = await _api.GetAsync(new Uri("/api/v1/me", UriKind.Relative), Ct);

        me.GetProperty("id").GetGuid().ShouldBe(user.Id);
        me.GetProperty("email").GetString().ShouldBe(user.Email);
        me.GetProperty("fullName").GetString().ShouldBe("Ana Souza");
        me.GetProperty("mfaMethod").GetString().ShouldBe("none");
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Totp_is_enabled_in_two_steps_and_then_required_at_login()
    {
        var user = await OidcTestClient.CreateUserAsync(_factory);

        var setup = await SendAsync(As(user.Id, HttpMethod.Post, "/api/v1/me/mfa/totp"), HttpStatusCode.OK);
        setup.GetProperty("authenticatorUri").GetString().ShouldStartWith("otpauth://totp/LexFlow%3A");
        var key = setup.GetProperty("sharedKey").GetString()!;
        var wrong = await SendAsync(As(user.Id, HttpMethod.Post, "/api/v1/me/mfa/totp/confirm", new { code = "000000" == Totp.Code(key, Now) ? "111111" : "000000" }), HttpStatusCode.BadRequest);
        wrong.GetProperty("code").GetString().ShouldBe("identity.mfa_code_invalid");
        var confirmed = await SendAsync(As(user.Id, HttpMethod.Post, "/api/v1/me/mfa/totp/confirm", new { code = Totp.Code(key, Now) }), HttpStatusCode.OK);
        confirmed.GetProperty("recoveryCodes").GetArrayLength().ShouldBe(10);
        (await SendAsync(As(user.Id, HttpMethod.Get, "/api/v1/me"), HttpStatusCode.OK)).GetProperty("mfaMethod").GetString().ShouldBe("totp");
        (await SendAsync(As(user.Id, HttpMethod.Post, "/api/v1/me/mfa/totp"), HttpStatusCode.Conflict)).GetProperty("code").GetString().ShouldBe("identity.mfa_already_enabled");

        var tokens = await OidcTestClient.SignInAsync(_browser, user.Email!, page => OidcTestClient.PostSecondFactorAsync(_browser, page, Totp.Code(key, Now)));

        OidcTestClient.JwtPayload(tokens.IdToken).GetProperty("amr").EnumerateArray().Select(a => a.GetString()).ShouldBe(["pwd", "mfa"]);
    }

    [Fact]
    public async Task Confirming_without_starting_the_setup_is_refused()
    {
        var user = await OidcTestClient.CreateUserAsync(_factory);

        var problem = await SendAsync(As(user.Id, HttpMethod.Post, "/api/v1/me/mfa/totp/confirm", new { code = "123456" }), HttpStatusCode.UnprocessableEntity);

        problem.GetProperty("code").GetString().ShouldBe("identity.mfa_setup_not_started");
    }

    [Fact]
    public async Task Wrong_second_factor_is_refused_and_a_recovery_code_works_only_once()
    {
        var user = await OidcTestClient.CreateUserAsync(_factory);
        var (_, recoveryCodes) = await EnableTotpAsync(user.Id);
        using var loggedIn = await OidcTestClient.PostLoginAsync(_browser, user.Email!, OidcTestClient.Password, "/");
        var secondFactorPage = loggedIn.Headers.Location!;

        using (var wrong = await OidcTestClient.PostSecondFactorAsync(_browser, secondFactorPage, "000000x"))
        {
            (await wrong.Content.ReadAsStringAsync(Ct)).ShouldContain("Código inválido ou expirado.");
        }

        var first = await OidcTestClient.SignInAsync(_browser, user.Email!, page => OidcTestClient.PostSecondFactorAsync(_browser, page, recoveryCodes[0], useRecoveryCode: true));
        first.AccessToken.ShouldNotBeNullOrEmpty();

        using var again = await OidcTestClient.PostLoginAsync(_browser, user.Email!, OidcTestClient.Password, "/");
        using var reused = await OidcTestClient.PostSecondFactorAsync(_browser, again.Headers.Location!, recoveryCodes[0], useRecoveryCode: true);
        (await reused.Content.ReadAsStringAsync(Ct)).ShouldContain("Código inválido ou expirado.");
    }

    [Fact]
    public async Task Email_code_is_sent_by_the_worker_at_each_login()
    {
        var user = await OidcTestClient.CreateUserAsync(_factory);
        await SendAsync(As(user.Id, HttpMethod.Post, "/api/v1/me/mfa/email"), HttpStatusCode.NoContent);
        (await SendAsync(As(user.Id, HttpMethod.Get, "/api/v1/me"), HttpStatusCode.OK)).GetProperty("mfaMethod").GetString().ShouldBe("email");
        await SendAsync(As(user.Id, HttpMethod.Post, "/api/v1/me/mfa/email"), HttpStatusCode.Conflict);

        var tokens = await OidcTestClient.SignInAsync(_browser, user.Email!, async page =>
            await OidcTestClient.PostSecondFactorAsync(_browser, page, await DeliverSignInCodeAsync(user.Id)));

        OidcTestClient.JwtPayload(tokens.IdToken).GetProperty("amr").EnumerateArray().Select(a => a.GetString()).ShouldContain("mfa");
    }

    [Fact]
    public async Task Open_sessions_are_listed_with_the_current_one_and_a_reused_refresh_hides_its_session()
    {
        var user = await OidcTestClient.CreateUserAsync(_factory);
        var first = await OidcTestClient.SignInAsync(_browser, user.Email!);
        using var otherBrowser = OidcTestClient.NewBrowser(_factory);
        var second = await OidcTestClient.SignInAsync(otherBrowser, user.Email!);
        var currentSession = Guid.Parse(OidcTestClient.JwtPayload(first.IdToken).GetProperty("sid").GetString()!);

        var sessions = await SendAsync(As(user.Id, HttpMethod.Get, "/api/v1/me/sessions", session: currentSession), HttpStatusCode.OK);

        sessions.GetArrayLength().ShouldBe(2);
        sessions.EnumerateArray().Single(s => s.GetProperty("current").GetBoolean()).GetProperty("id").GetGuid().ShouldBe(currentSession);

        using (var rotated = await OidcTestClient.RefreshAsync(otherBrowser, second.RefreshToken))
        {
            rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var reuse = await OidcTestClient.RefreshAsync(otherBrowser, second.RefreshToken))
        {
            reuse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var afterReuse = await SendAsync(As(user.Id, HttpMethod.Get, "/api/v1/me/sessions", session: currentSession), HttpStatusCode.OK);
        afterReuse.EnumerateArray().Select(s => s.GetProperty("id").GetGuid()).ShouldBe([currentSession]);
    }

    [Fact]
    public async Task A_user_ends_their_own_session_but_not_someone_elses()
    {
        var owner = await OidcTestClient.CreateUserAsync(_factory);
        var stranger = await OidcTestClient.CreateUserAsync(_factory);
        var tokens = await OidcTestClient.SignInAsync(_browser, owner.Email!);
        var session = Guid.Parse(OidcTestClient.JwtPayload(tokens.IdToken).GetProperty("sid").GetString()!);

        var notFound = await SendAsync(As(stranger.Id, HttpMethod.Delete, $"/api/v1/me/sessions/{session}"), HttpStatusCode.NotFound);
        notFound.GetProperty("code").GetString().ShouldBe("identity.session_not_found");
        await SendAsync(As(owner.Id, HttpMethod.Delete, $"/api/v1/me/sessions/{session}"), HttpStatusCode.NoContent);

        using var refresh = await OidcTestClient.RefreshAsync(_browser, tokens.RefreshToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(As(owner.Id, HttpMethod.Get, "/api/v1/me/sessions"), HttpStatusCode.OK)).GetArrayLength().ShouldBe(0);
    }
}

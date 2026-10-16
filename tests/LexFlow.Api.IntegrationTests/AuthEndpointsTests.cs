using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Emails;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Endpoints de conta (Etapa 2, PR A) pela API real, com o envio de e-mail feito por um "Worker" separado.</summary>
public sealed class AuthEndpointsTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private const string Password = "uma senha bem longa";

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;

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
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private static string NewEmail() => $"{Guid.CreateVersion7():N}@exemplo.com.br";

    private Task<HttpResponseMessage> PostAsync(string path, object body) =>
        _client.PostAsJsonAsync(new Uri($"/api/v1/auth/{path}", UriKind.Relative), body, Ct);

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;

    private AccountEmailRequestedIntegrationEvent[] AccountEmails(Guid userId) =>
        [.. _factory.PublishedEvents.OfType<AccountEmailRequestedIntegrationEvent>().Where(e => e.UserId == userId)];

    private async Task<ApplicationUser?> FindUserAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
    }

    /// <summary>
    /// Faz o papel do Worker para o evento publicado e devolve (userId, token) do link enviado. A cópia gravada pela
    /// fábrica é a de antes do publicador preencher os metadados, então ganha um <c>EventId</c> próprio aqui.
    /// </summary>
    private async Task<(Guid UserId, string Token)> DeliverAsync(AccountEmailRequestedIntegrationEvent integrationEvent)
    {
        var email = new FakeEmailSender();
        await using (var worker = dependencies.WorkerServices(email))
        await using (var scope = worker.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AccountEmailConsumer>().Handle(integrationEvent with { EventId = Guid.CreateVersion7() }, Ct);
        }

        var html = email.Sent.Single().HtmlBody;
        var start = html.IndexOf("href=\"", StringComparison.Ordinal) + "href=\"".Length;
        var query = HttpUtility.ParseQueryString(new Uri(HttpUtility.HtmlDecode(html[start..html.IndexOf('"', start)])).Query);
        return (Guid.Parse(query["userId"]!), query["token"]!);
    }

    [Fact]
    public async Task Register_creates_an_unconfirmed_account_and_asks_for_the_confirmation_email()
    {
        var email = NewEmail();

        using var response = await PostAsync("register", new { email = email.ToUpperInvariant(), fullName = "Ana Souza", password = Password });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var user = (await FindUserAsync(email)).ShouldNotBeNull();
        user.Email.ShouldBe(email);
        user.EmailConfirmed.ShouldBeFalse();
        AccountEmails(user.Id).Select(e => e.Kind).ShouldBe([AccountEmailKind.ConfirmEmail]);
    }

    [Fact]
    public async Task Registering_an_existing_email_looks_the_same_and_warns_the_owner()
    {
        var email = NewEmail();
        (await PostAsync("register", new { email, fullName = "Ana Souza", password = Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        using var again = await PostAsync("register", new { email, fullName = "Outra Pessoa", password = "outra senha bem longa" });

        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var user = (await FindUserAsync(email))!;
        user.FullName.ShouldBe("Ana Souza");
        AccountEmails(user.Id).Select(e => e.Kind).ShouldBe([AccountEmailKind.ConfirmEmail, AccountEmailKind.AlreadyRegistered]);
    }

    [Fact]
    public async Task Short_password_is_rejected_with_its_code_even_for_an_existing_email()
    {
        using var response = await PostAsync("register", new { email = NewEmail(), fullName = "Ana", password = "curta-11ch" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemAsync(response)).GetProperty("code").GetString().ShouldBe("identity.password_too_short");
    }

    [Fact]
    public async Task Invalid_fields_are_reported_per_field()
    {
        using var response = await PostAsync("register", new { email = "sem-arroba", fullName = "", password = Password });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await ProblemAsync(response)).GetProperty("errors");
        errors.GetProperty("email")[0].GetProperty("code").GetString().ShouldBe("identity.email_invalid");
        errors.GetProperty("fullName")[0].GetProperty("code").GetString().ShouldBe("identity.full_name_required");
    }

    [Fact]
    public async Task Link_sent_by_the_worker_confirms_the_email()
    {
        var email = NewEmail();
        (await PostAsync("register", new { email, fullName = "Ana Souza", password = Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var user = (await FindUserAsync(email))!;
        var (userId, token) = await DeliverAsync(AccountEmails(user.Id).Single());

        using var response = await PostAsync("confirm-email", new { userId, token });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindUserAsync(email))!.EmailConfirmed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("bm90LWEtdG9rZW4")]
    [InlineData("isto não é base64")]
    public async Task Invalid_or_tampered_token_is_rejected(string token)
    {
        var email = NewEmail();
        (await PostAsync("register", new { email, fullName = "Ana Souza", password = Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var user = (await FindUserAsync(email))!;

        using var confirm = await PostAsync("confirm-email", new { userId = user.Id, token });
        using var unknownUser = await PostAsync("reset-password", new { userId = Guid.CreateVersion7(), token, newPassword = Password });

        confirm.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemAsync(confirm)).GetProperty("code").GetString().ShouldBe("identity.invalid_token");
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemAsync(unknownUser)).GetProperty("code").GetString().ShouldBe("identity.invalid_token");
    }

    [Fact]
    public async Task Forgot_password_resets_with_a_single_use_link()
    {
        var email = NewEmail();
        (await PostAsync("register", new { email, fullName = "Ana Souza", password = Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var user = (await FindUserAsync(email))!;
        var confirmation = await DeliverAsync(AccountEmails(user.Id).Single());
        (await PostAsync("confirm-email", new { userId = confirmation.UserId, token = confirmation.Token })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await PostAsync("forgot-password", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var request = AccountEmails(user.Id).Last();
        request.Kind.ShouldBe(AccountEmailKind.ResetPassword);
        var (userId, token) = await DeliverAsync(request);

        using var reset = await PostAsync("reset-password", new { userId, token, newPassword = "a nova senha bem longa" });
        using var reused = await PostAsync("reset-password", new { userId, token, newPassword = "mais uma senha longa" });

        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        reused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemAsync(reused)).GetProperty("code").GetString().ShouldBe("identity.invalid_token");
        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.CheckPasswordAsync((await users.FindByIdAsync(userId.ToString()))!, "a nova senha bem longa")).ShouldBeTrue();
    }

    [Fact]
    public async Task Forgot_password_resends_confirmation_to_unconfirmed_accounts_and_ignores_unknown_emails()
    {
        var email = NewEmail();
        (await PostAsync("register", new { email, fullName = "Ana Souza", password = Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var user = (await FindUserAsync(email))!;
        var before = _factory.PublishedEvents.Count;

        using var known = await PostAsync("forgot-password", new { email });
        using var unknown = await PostAsync("forgot-password", new { email = NewEmail() });

        known.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        unknown.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _factory.PublishedEvents.Count.ShouldBe(before + 1);
        AccountEmails(user.Id).Last().Kind.ShouldBe(AccountEmailKind.ConfirmEmail);
    }

    [Fact]
    public async Task Reset_with_a_valid_link_still_enforces_the_password_policy()
    {
        var email = NewEmail();
        (await PostAsync("register", new { email, fullName = "Ana Souza", password = Password })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var user = (await FindUserAsync(email))!;
        var (userId, token) = await DeliverAsync(new AccountEmailRequestedIntegrationEvent(user.Id, AccountEmailKind.ResetPassword));

        using var response = await PostAsync("reset-password", new { userId, token, newPassword = "curta-11ch" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemAsync(response)).GetProperty("code").GetString().ShouldBe("identity.password_too_short");
    }

    [Theory]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/auth/confirm-email")]
    [InlineData("/api/v1/auth/forgot-password")]
    [InlineData("/api/v1/auth/reset-password")]
    public void Account_endpoints_are_public_on_purpose(string route)
    {
        var endpoint = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == route);

        endpoint.Metadata.GetMetadata<PublicEndpointAttribute>()!.Reason.ShouldNotBeNullOrWhiteSpace();
    }
}

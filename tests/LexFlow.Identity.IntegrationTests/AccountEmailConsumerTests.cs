using System.Web;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Application.Accounts;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Emails;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Identity.IntegrationTests;

/// <summary>Envio dos e-mails de conta pelo Worker, com banco real e o anel de chaves compartilhado pelo banco.</summary>
public sealed class AccountEmailConsumerTests(IdentityDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly FakeEmailSender _email = new();

    private async Task<ApplicationUser> CreateUserAsync(bool confirmed = false)
    {
        await using var provider = IdentityDatabaseFixture.BuildProvider(fixture.ConnectionString, _email);
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = ApplicationUser.Register(Guid.CreateVersion7(), $"{Guid.CreateVersion7():N}@exemplo.com.br", "Ana Souza", new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        user.EmailConfirmed = confirmed;
        (await users.CreateAsync(user, "senha-longa-de-teste")).Succeeded.ShouldBeTrue();
        return user;
    }

    /// <summary>Consome o evento num "Worker" próprio (container de DI separado).</summary>
    private async Task ConsumeAsync(AccountEmailRequestedIntegrationEvent integrationEvent)
    {
        await using var worker = IdentityDatabaseFixture.BuildProvider(fixture.ConnectionString, _email);
        await using var scope = worker.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccountEmailConsumer>().Handle(integrationEvent, Ct);
    }

    private static AccountEmailRequestedIntegrationEvent Event(Guid userId, AccountEmailKind kind) =>
        new(userId, kind) { EventId = Guid.CreateVersion7(), CorrelationId = "identity-tests" };

    private static string TokenFrom(EmailMessage email)
    {
        const string marker = "href=\"";
        var start = email.HtmlBody.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var link = new Uri(HttpUtility.HtmlDecode(email.HtmlBody[start..email.HtmlBody.IndexOf('"', start)]));
        return AccountTokens.Decode(HttpUtility.ParseQueryString(link.Query)["token"])!;
    }

    [Fact]
    public async Task Confirmation_token_generated_by_the_worker_is_accepted_by_another_process()
    {
        var user = await CreateUserAsync();

        await ConsumeAsync(Event(user.Id, AccountEmailKind.ConfirmEmail));

        var email = _email.Sent.Single();
        email.To.ShouldBe([user.Email!]);
        await using var api = IdentityDatabaseFixture.BuildProvider(fixture.ConnectionString, new FakeEmailSender());
        await using var scope = api.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await users.FindByIdAsync(user.Id.ToString());
        (await users.ConfirmEmailAsync(stored!, TokenFrom(email))).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_token_works_once_because_the_password_change_renews_the_security_stamp()
    {
        var user = await CreateUserAsync(confirmed: true);
        await ConsumeAsync(Event(user.Id, AccountEmailKind.ResetPassword));
        var token = TokenFrom(_email.Sent.Single());

        await using var api = IdentityDatabaseFixture.BuildProvider(fixture.ConnectionString, new FakeEmailSender());
        await using var scope = api.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = (await users.FindByIdAsync(user.Id.ToString()))!;

        (await users.ResetPasswordAsync(stored, token, "outra-senha-bem-longa")).Succeeded.ShouldBeTrue();
        (await users.ResetPasswordAsync(stored, token, "mais-uma-senha-longa")).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Already_registered_notice_carries_no_token()
    {
        var user = await CreateUserAsync(confirmed: true);

        await ConsumeAsync(Event(user.Id, AccountEmailKind.AlreadyRegistered));

        var email = _email.Sent.Single();
        email.Subject.ShouldBe("Você já tem uma conta no LexFlow");
        email.HtmlBody.ShouldNotContain("token=");
    }

    [Fact]
    public async Task Confirmed_user_does_not_get_another_confirmation_and_unknown_user_gets_nothing()
    {
        var confirmed = await CreateUserAsync(confirmed: true);

        await ConsumeAsync(Event(confirmed.Id, AccountEmailKind.ConfirmEmail));
        await ConsumeAsync(Event(Guid.CreateVersion7(), AccountEmailKind.ResetPassword));

        _email.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Redelivered_event_sends_a_single_email()
    {
        var user = await CreateUserAsync();
        var integrationEvent = Event(user.Id, AccountEmailKind.ConfirmEmail);

        await ConsumeAsync(integrationEvent);
        await ConsumeAsync(integrationEvent);

        _email.Sent.Count.ShouldBe(1);
        (await fixture.ScalarAsync<long>("SELECT count(*) FROM identity.processed_integration_events WHERE event_id = $1", integrationEvent.EventId)).ShouldBe(1);
    }

    [Fact]
    public async Task Audit_trail_records_the_user_without_secrets_and_never_the_keys()
    {
        var user = await CreateUserAsync();
        await ConsumeAsync(Event(user.Id, AccountEmailKind.ConfirmEmail));

        var changes = await fixture.ScalarAsync<string>(
            "SELECT changes::text FROM audit.audit_logs WHERE entity_type = 'ApplicationUser' AND entity_id = $1 AND action = 'Created'",
            user.Id.ToString());

        changes.ShouldNotBeNull();
        changes.ShouldContain("fullName");
        changes.ShouldNotContain("passwordHash");
        changes.ShouldNotContain("securityStamp");
        changes.ShouldNotContain("concurrencyStamp");
        (await fixture.ScalarAsync<long>("SELECT count(*) FROM audit.audit_logs WHERE entity_type = 'DataProtectionKey'")).ShouldBe(0);
    }
}

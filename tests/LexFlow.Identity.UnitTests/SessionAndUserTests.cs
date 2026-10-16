using LexFlow.Identity.Domain.Sessions;
using LexFlow.Identity.Domain.Users;

namespace LexFlow.Identity.UnitTests;

public sealed class SessionAndUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static Session NewSession() => Session.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), "203.0.113.7", "Firefox", Now);

    [Fact]
    public void Session_expires_after_14_days_without_use()
    {
        var session = NewSession();

        session.IsActive(Now.AddDays(14).AddSeconds(-1)).ShouldBeTrue();
        session.IsActive(Now.AddDays(14)).ShouldBeFalse();
    }

    [Fact]
    public void Use_renews_the_idle_window_up_to_the_30_day_limit()
    {
        var session = NewSession();

        session.Touch(Now.AddDays(13));
        session.Touch(Now.AddDays(26));

        session.ExpiresAt.ShouldBe(Now.AddDays(30));
        session.IsActive(Now.AddDays(29)).ShouldBeTrue();
        session.IsActive(Now.AddDays(30)).ShouldBeFalse();
    }

    [Fact]
    public void Expired_or_revoked_session_is_not_reopened_by_use()
    {
        var expired = NewSession();
        expired.Touch(Now.AddDays(15));
        expired.LastSeenAt.ShouldBe(Now);

        var revoked = NewSession();
        revoked.Revoke(Now.AddHours(1), "logout");
        revoked.Touch(Now.AddHours(2));
        revoked.LastSeenAt.ShouldBe(Now);
        revoked.IsActive(Now.AddHours(2)).ShouldBeFalse();
    }

    [Fact]
    public void First_revocation_wins()
    {
        var session = NewSession();

        session.Revoke(Now.AddHours(1), "logout");
        session.Revoke(Now.AddHours(2), "password_reset");

        session.RevokedAt.ShouldBe(Now.AddHours(1));
        session.RevokedReason.ShouldBe("logout");
    }

    [Fact]
    public void Session_keeps_origin_truncating_long_user_agent_and_links_authorization()
    {
        var session = Session.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), "203.0.113.7", new string('a', 600), Now);
        session.AttachAuthorization("auth-1");

        session.UserAgent!.Length.ShouldBe(Session.MaxUserAgentLength);
        session.IpAddress.ShouldBe("203.0.113.7");
        session.AuthorizationId.ShouldBe("auth-1");
        session.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Registered_user_logs_in_with_email_and_starts_unconfirmed()
    {
        var id = Guid.CreateVersion7();

        var user = ApplicationUser.Register(id, "ana@exemplo.com.br", "  Ana Souza ", Now);
        user.Rename(" Ana S. Souza ");
        user.RecordLogin(Now.AddMinutes(5));

        user.Id.ShouldBe(id);
        user.UserName.ShouldBe("ana@exemplo.com.br");
        user.Email.ShouldBe("ana@exemplo.com.br");
        user.EmailConfirmed.ShouldBeFalse();
        user.FullName.ShouldBe("Ana S. Souza");
        user.CreatedAt.ShouldBe(Now);
        user.LastLoginAt.ShouldBe(Now.AddMinutes(5));
        user.SecurityStamp.ShouldNotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("", "Ana")]
    [InlineData("ana@exemplo.com.br", " ")]
    public void User_requires_email_and_name(string email, string name) =>
        Should.Throw<ArgumentException>(() => ApplicationUser.Register(Guid.CreateVersion7(), email, name, Now));
}

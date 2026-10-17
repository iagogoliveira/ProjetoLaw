using System.Web;
using LexFlow.Identity.Application.Accounts;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure;
using LexFlow.Identity.Infrastructure.Emails;

namespace LexFlow.Identity.UnitTests;

public sealed class AccountEmailComposerTests
{
    private static readonly AccountEmailComposer Composer = new(new IdentitySettings { PublicUrl = new Uri("https://app.lexflow.test") });

    private static ApplicationUser NewUser(string name = "Ana Souza") =>
        ApplicationUser.Register(Guid.CreateVersion7(), "ana@exemplo.com.br", name, DateTimeOffset.UnixEpoch);

    /// <summary>O link do botão, lido do HTML.</summary>
    private static Uri LinkIn(string html)
    {
        const string marker = "href=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return new Uri(HttpUtility.HtmlDecode(html[start..html.IndexOf('"', start)]));
    }

    [Fact]
    public void Confirmation_email_links_to_the_confirmation_page_with_user_and_encoded_token()
    {
        var user = NewUser();

        var email = Composer.Compose(AccountEmailKind.ConfirmEmail, user, "CfDJ8+abc/def=");

        email.To.ShouldBe(["ana@exemplo.com.br"]);
        email.Subject.ShouldBe("Confirme seu e-mail no LexFlow");
        email.HtmlBody.ShouldContain("lang=\"pt-BR\"");
        email.HtmlBody.ShouldContain("Olá, Ana Souza.");
        var link = LinkIn(email.HtmlBody);
        link.GetLeftPart(UriPartial.Path).ShouldBe("https://app.lexflow.test" + AccountLinks.ConfirmEmailPath);
        var query = HttpUtility.ParseQueryString(link.Query);
        query["userId"].ShouldBe(user.Id.ToString());
        AccountTokens.Decode(query["token"]).ShouldBe("CfDJ8+abc/def=");
    }

    [Fact]
    public void Reset_email_links_to_the_reset_page()
    {
        var email = Composer.Compose(AccountEmailKind.ResetPassword, NewUser(), "token");

        email.Subject.ShouldBe("Redefinição de senha do LexFlow");
        LinkIn(email.HtmlBody).AbsolutePath.ShouldBe(AccountLinks.ResetPasswordPath);
    }

    [Fact]
    public void Already_registered_notice_points_to_password_recovery_without_token()
    {
        var email = Composer.Compose(AccountEmailKind.AlreadyRegistered, NewUser(), token: null);

        email.Subject.ShouldBe("Você já tem uma conta no LexFlow");
        LinkIn(email.HtmlBody).ShouldBe(new Uri("https://app.lexflow.test" + AccountLinks.ForgotPasswordPath));
    }

    [Fact]
    public void User_supplied_name_is_html_escaped()
    {
        var email = Composer.Compose(AccountEmailKind.AlreadyRegistered, NewUser("<script>alert(1)</script>"), token: null);

        email.HtmlBody.ShouldNotContain("<script>");
        email.HtmlBody.ShouldContain("&lt;script&gt;");
    }

    [Theory]
    [InlineData(AccountEmailKind.ConfirmEmail)]
    [InlineData(AccountEmailKind.ResetPassword)]
    public void Token_emails_require_a_token(AccountEmailKind kind) =>
        Should.Throw<ArgumentException>(() => Composer.Compose(kind, NewUser(), token: null));

    [Fact]
    public void Sign_in_code_goes_in_the_body_without_any_link()
    {
        var email = Composer.Compose(AccountEmailKind.SignInCode, NewUser(), "482913");

        email.Subject.ShouldBe("Seu código de acesso ao LexFlow");
        email.HtmlBody.ShouldContain("482913");
        email.HtmlBody.ShouldNotContain("href=");
    }
}

using LexFlow.Identity.Application.Accounts;
using LexFlow.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace LexFlow.Identity.UnitTests;

public sealed class AccountRulesTests
{
    private static readonly IdentityErrorDescriber Describer = new();

    [Theory]
    [InlineData("CfDJ8+token/with=padding")]
    [InlineData("ção-ü")]
    public void Token_survives_the_link_encoding(string token)
    {
        var encoded = AccountTokens.Encode(token);

        encoded.ShouldNotContain("+");
        encoded.ShouldNotContain("/");
        encoded.ShouldNotContain("=");
        AccountTokens.Decode(encoded).ShouldBe(token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("não é base64!")]
    public void Malformed_token_decodes_to_null(string? encoded) => AccountTokens.Decode(encoded).ShouldBeNull();

    [Fact]
    public void Identity_failures_map_to_stable_module_codes()
    {
        PasswordPolicy.ToError(IdentityResult.Failed(Describer.InvalidToken())).ShouldBe(IdentityErrors.InvalidToken);
        PasswordPolicy.ToError(IdentityResult.Failed(Describer.PasswordTooShort(PasswordPolicy.MinLength))).ShouldBe(IdentityErrors.PasswordTooShort);
        PasswordPolicy.ToError(IdentityResult.Failed(new IdentityError { Code = PasswordPolicy.BreachedErrorCode })).ShouldBe(IdentityErrors.PasswordBreached);
        PasswordPolicy.ToError(IdentityResult.Failed(Describer.PasswordRequiresDigit())).ShouldBe(IdentityErrors.PasswordRejected);
    }

    [Fact]
    public void Commands_never_print_secrets()
    {
        new RegisterUserCommand("ana@exemplo.com.br", "Ana", "segredo-muito-longo").ToString().ShouldNotContain("segredo");
        new ResetPasswordCommand(Guid.CreateVersion7(), "token-secreto", "segredo-muito-longo").ToString()
            .ShouldSatisfyAllConditions(s => s.ShouldNotContain("segredo"), s => s.ShouldNotContain("token-secreto"));
    }

    [Fact]
    public async Task Register_validation_reports_each_field_with_its_code()
    {
        var result = await new RegisterUserValidator().ValidateAsync(
            new RegisterUserCommand("sem-arroba", "", new string('x', PasswordPolicy.MaxLength + 1)),
            TestContext.Current.CancellationToken);

        result.Errors.Select(e => e.ErrorCode).ShouldBe(
            ["identity.email_invalid", "identity.full_name_required", "identity.password_too_long"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Valid_requests_pass_validation()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();

        (await new RegisterUserValidator().ValidateAsync(new RegisterUserCommand("ana@exemplo.com.br", "Ana", "senha-com-doze"), ct)).IsValid.ShouldBeTrue();
        (await new ForgotPasswordValidator().ValidateAsync(new ForgotPasswordCommand("ana@exemplo.com.br"), ct)).IsValid.ShouldBeTrue();
        (await new ConfirmEmailValidator().ValidateAsync(new ConfirmEmailCommand(id, "t"), ct)).IsValid.ShouldBeTrue();
        (await new ResetPasswordValidator().ValidateAsync(new ResetPasswordCommand(id, "t", "senha-com-doze"), ct)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Token_requests_require_user_and_token()
    {
        var ct = TestContext.Current.CancellationToken;

        var confirm = await new ConfirmEmailValidator().ValidateAsync(new ConfirmEmailCommand(Guid.Empty, ""), ct);
        var reset = await new ResetPasswordValidator().ValidateAsync(new ResetPasswordCommand(Guid.Empty, "", ""), ct);
        var forgot = await new ForgotPasswordValidator().ValidateAsync(new ForgotPasswordCommand("x"), ct);

        confirm.Errors.Select(e => e.ErrorCode).ShouldBe(["identity.user_id_required", "identity.token_required"], ignoreOrder: true);
        reset.Errors.Select(e => e.ErrorCode).ShouldBe(["identity.user_id_required", "identity.token_required", "identity.password_required"], ignoreOrder: true);
        forgot.Errors.Single().ErrorCode.ShouldBe("identity.email_invalid");
    }

    [Theory]
    [InlineData("123 456", "123456")]
    [InlineData("123-456", "123456")]
    [InlineData("123456", "123456")]
    public void Mfa_codes_accept_the_spacing_shown_by_authenticator_apps(string typed, string expected) =>
        LexFlow.Identity.Application.Mfa.MfaCodes.Normalize(typed).ShouldBe(expected);
}

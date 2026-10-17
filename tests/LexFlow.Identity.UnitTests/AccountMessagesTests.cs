using LexFlow.Identity.Domain;
using LexFlow.Identity.Endpoints.Pages;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Identity.UnitTests;

public sealed class AccountMessagesTests
{
    [Fact]
    public void Validation_errors_become_one_portuguese_line_per_problem()
    {
        var error = new ValidationError(
        [
            new FieldError("email", "identity.email_invalid", "'Email' is not valid."),
            new FieldError("token", "identity.token_required", "required"),
            new FieldError("userId", "identity.user_id_required", "required"),
        ]);

        AccountMessages.For(error).ShouldBe(["Informe um e-mail válido.", AccountMessages.LinkInvalid]);
    }

    [Fact]
    public void Business_errors_use_their_own_message() =>
        AccountMessages.For(IdentityErrors.PasswordBreached).ShouldBe([IdentityErrors.PasswordBreached.Message]);
}

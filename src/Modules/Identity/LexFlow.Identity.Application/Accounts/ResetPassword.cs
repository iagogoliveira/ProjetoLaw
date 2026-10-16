using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Domain;
using LexFlow.Identity.Domain.Users;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;

namespace LexFlow.Identity.Application.Accounts;

/// <summary>Redefine a senha com o token do link (Base64Url). O token é de uso único: a troca renova o security stamp.</summary>
public sealed record ResetPasswordCommand(Guid UserId, string Token, string NewPassword) : ICommand
{
    /// <summary>Token e senha nunca aparecem em logs ou mensagens de exceção.</summary>
    public override string ToString() => $"{nameof(ResetPasswordCommand)} {{ UserId = {UserId} }}";
}

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(c => c.UserId).NotEmpty().WithErrorCode("identity.user_id_required");
        RuleFor(c => c.Token).NotEmpty().WithErrorCode("identity.token_required");
        RuleFor(c => c.NewPassword).NewPassword();
    }
}

internal sealed class ResetPasswordHandler(UserManager<ApplicationUser> users) : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var token = AccountTokens.Decode(command.Token);
        if (token is null || await users.FindByIdAsync(command.UserId.ToString()) is not { } user)
        {
            return IdentityErrors.InvalidToken;
        }

        var reset = await users.ResetPasswordAsync(user, token, command.NewPassword);
        return reset.Succeeded ? Unit.Value : PasswordPolicy.ToError(reset);
    }
}

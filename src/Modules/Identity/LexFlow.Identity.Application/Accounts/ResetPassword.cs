using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Application.Sessions;
using LexFlow.Identity.Domain;
using LexFlow.Identity.Domain.Users;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;

namespace LexFlow.Identity.Application.Accounts;

/// <summary>
/// Redefine a senha com o token do link (Base64Url). O token é de uso único: a troca renova o security stamp. Todas as
/// sessões abertas são encerradas, e quem estava com a senha antiga precisa entrar de novo.
/// </summary>
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

internal sealed class ResetPasswordHandler(
    UserManager<ApplicationUser> users,
    IIdentityDbContext db,
    ISessionTokenRevoker tokens,
    IClock clock)
    : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var token = AccountTokens.Decode(command.Token);
        if (token is null || await users.FindByIdAsync(command.UserId.ToString()) is not { } user)
        {
            return IdentityErrors.InvalidToken;
        }

        var reset = await users.ResetPasswordAsync(user, token, command.NewPassword);
        if (!reset.Succeeded)
        {
            return PasswordPolicy.ToError(reset);
        }

        await SessionTermination.EndAllAsync(user.Id, SessionEndReasons.PasswordReset, db, tokens, clock, cancellationToken);
        return Unit.Value;
    }
}

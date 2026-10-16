using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Domain;
using LexFlow.Identity.Domain.Users;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;

namespace LexFlow.Identity.Application.Accounts;

/// <summary>Confirma o e-mail com o token do link enviado no cadastro (Base64Url, ver <see cref="AccountTokens"/>).</summary>
public sealed record ConfirmEmailCommand(Guid UserId, string Token) : ICommand;

internal sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator()
    {
        RuleFor(c => c.UserId).NotEmpty().WithErrorCode("identity.user_id_required");
        RuleFor(c => c.Token).NotEmpty().WithErrorCode("identity.token_required");
    }
}

/// <summary>Usuário inexistente e token inválido dão o mesmo erro, para o link não servir de sonda de contas.</summary>
internal sealed class ConfirmEmailHandler(UserManager<ApplicationUser> users) : ICommandHandler<ConfirmEmailCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        var token = AccountTokens.Decode(command.Token);
        if (token is null || await users.FindByIdAsync(command.UserId.ToString()) is not { } user)
        {
            return IdentityErrors.InvalidToken;
        }

        if (user.EmailConfirmed)
        {
            return Unit.Value;
        }

        var confirmed = await users.ConfirmEmailAsync(user, token);
        return confirmed.Succeeded ? Unit.Value : IdentityErrors.InvalidToken;
    }
}

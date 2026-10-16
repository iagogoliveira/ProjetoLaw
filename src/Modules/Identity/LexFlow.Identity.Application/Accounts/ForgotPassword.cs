using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain.Users;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using EmailAddress = LexFlow.SharedKernel.ValueObjects.Email;

namespace LexFlow.Identity.Application.Accounts;

public sealed record ForgotPasswordCommand(string Email) : ICommand;

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(c => c.Email).Must(EmailAddress.IsValid).WithErrorCode("identity.email_invalid");
    }
}

/// <summary>
/// Sempre responde sucesso. Conta com e-mail confirmado recebe o link de redefinição; conta ainda não confirmada
/// recebe de novo a confirmação (a senha só pode ser redefinida depois dela); e-mail sem conta não recebe nada.
/// </summary>
internal sealed class ForgotPasswordHandler(
    UserManager<ApplicationUser> users,
    IIdentityDbContext db,
    IIntegrationEventPublisher events)
    : ICommandHandler<ForgotPasswordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindByEmailAsync(EmailAddress.Create(command.Email).Value.Value) is not { } user)
        {
            return Unit.Value;
        }

        var kind = user.EmailConfirmed ? AccountEmailKind.ResetPassword : AccountEmailKind.ConfirmEmail;
        events.Publish(new AccountEmailRequestedIntegrationEvent(user.Id, kind));
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

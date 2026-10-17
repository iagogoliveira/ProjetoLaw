using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain.Users;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Infrastructure.Common.Messaging;
using Microsoft.AspNetCore.Identity;

namespace LexFlow.Identity.Infrastructure.Emails;

/// <summary>
/// Envia, no Worker, os e-mails pedidos pela API. O token é gerado aqui, na hora do envio (a chave de Data Protection é
/// compartilhada pelo banco). O envio é "pelo menos uma vez": se a gravação do processamento falhar depois do envio,
/// a reentrega manda o e-mail de novo, com um token novo e igualmente válido.
/// </summary>
public sealed class AccountEmailConsumer(
    IdentityModuleDbContext db,
    IClock clock,
    UserManager<ApplicationUser> users,
    AccountEmailComposer composer,
    IEmailSender email)
    : IntegrationEventConsumer<AccountEmailRequestedIntegrationEvent, IdentityModuleDbContext>(db, clock)
{
    protected override async Task ConsumeAsync(AccountEmailRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (await users.FindByIdAsync(integrationEvent.UserId.ToString()) is not { Email: not null } user)
        {
            return;
        }

        string? token;
        switch (integrationEvent.Kind)
        {
            case AccountEmailKind.ConfirmEmail when user.EmailConfirmed:
                return;
            case AccountEmailKind.ConfirmEmail:
                token = await users.GenerateEmailConfirmationTokenAsync(user);
                break;
            case AccountEmailKind.ResetPassword:
                token = await users.GeneratePasswordResetTokenAsync(user);
                break;
            case AccountEmailKind.SignInCode:
                token = await users.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);
                break;
            default:
                token = null;
                break;
        }

        await email.SendAsync(composer.Compose(integrationEvent.Kind, user, token), cancellationToken);
    }
}

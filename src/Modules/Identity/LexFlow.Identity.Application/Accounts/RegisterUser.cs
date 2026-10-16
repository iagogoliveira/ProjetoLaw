using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain.Users;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using EmailAddress = LexFlow.SharedKernel.ValueObjects.Email;

namespace LexFlow.Identity.Application.Accounts;

/// <summary>Cadastro aberto (decisão de 2026-10-16): cria a conta com o e-mail ainda não confirmado.</summary>
public sealed record RegisterUserCommand(string Email, string FullName, string Password) : ICommand
{
    /// <summary>A senha nunca aparece em logs ou mensagens de exceção.</summary>
    public override string ToString() => $"{nameof(RegisterUserCommand)} {{ Email = {Email} }}";
}

internal sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(c => c.Email).Must(EmailAddress.IsValid).WithErrorCode("identity.email_invalid");
        RuleFor(c => c.FullName).NotEmpty().WithErrorCode("identity.full_name_required")
            .MaximumLength(ApplicationUser.MaxFullNameLength).WithErrorCode("identity.full_name_too_long");
        RuleFor(c => c.Password).NewPassword();
    }
}

/// <summary>
/// A resposta é a mesma quando o e-mail já tem conta: o dono do e-mail recebe um aviso e quem tentou o cadastro não
/// descobre quais e-mails existem (ADR 0013). A senha é validada antes, para o resultado não depender da existência.
/// </summary>
internal sealed class RegisterUserHandler(
    UserManager<ApplicationUser> users,
    IIdentityDbContext db,
    IIntegrationEventPublisher events,
    IIdGenerator ids,
    IClock clock)
    : ICommandHandler<RegisterUserCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var email = EmailAddress.Create(command.Email).Value.Value;
        var user = ApplicationUser.Register(ids.NewId(), email, command.FullName, clock.UtcNow);

        foreach (var validator in users.PasswordValidators)
        {
            var check = await validator.ValidateAsync(users, user, command.Password);
            if (!check.Succeeded)
            {
                return PasswordPolicy.ToError(check);
            }
        }

        if (await users.FindByEmailAsync(email) is { } existing)
        {
            events.Publish(new AccountEmailRequestedIntegrationEvent(existing.Id, AccountEmailKind.AlreadyRegistered));
            await db.SaveChangesAsync(cancellationToken);
            return Unit.Value;
        }

        // Senha já validada: grava o hash direto, sem repetir os validadores (e a consulta de vazamentos).
        user.PasswordHash = users.PasswordHasher.HashPassword(user, command.Password);
        events.Publish(new AccountEmailRequestedIntegrationEvent(user.Id, AccountEmailKind.ConfirmEmail));
        var created = await users.CreateAsync(user);
        return created.Succeeded ? Unit.Value : PasswordPolicy.ToError(created);
    }
}

using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain;
using LexFlow.Identity.Domain.Users;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;

namespace LexFlow.Identity.Application.Mfa;

/// <summary>Chave para o aplicativo autenticador: digitada à mão ou lida pelo QR code gerado a partir do <c>otpauth://</c>.</summary>
public sealed record TotpSetupResponse(string SharedKey, string AuthenticatorUri);

public sealed record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);

/// <summary>Primeira etapa do TOTP: gera uma chave nova. O MFA só liga na confirmação com um código válido.</summary>
public sealed record StartTotpSetupCommand(Guid UserId) : ICommand<TotpSetupResponse>;

internal sealed class StartTotpSetupHandler(UserManager<ApplicationUser> users) : ICommandHandler<StartTotpSetupCommand, TotpSetupResponse>
{
    public const string Issuer = "LexFlow";

    public async Task<Result<TotpSetupResponse>> Handle(StartTotpSetupCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(command.UserId.ToString()) is not { } user)
        {
            return IdentityErrors.UserNotFound;
        }

        if (user.TwoFactorEnabled)
        {
            return IdentityErrors.MfaAlreadyEnabled;
        }

        await users.ResetAuthenticatorKeyAsync(user);
        var key = (await users.GetAuthenticatorKeyAsync(user))!;
        var label = Uri.EscapeDataString($"{Issuer}:{user.Email}");
        return new TotpSetupResponse(key, $"otpauth://totp/{label}?secret={key}&issuer={Issuer}&digits=6");
    }
}

/// <summary>Segunda etapa: o código do aplicativo prova a configuração; liga o MFA e devolve 10 códigos de recuperação.</summary>
public sealed record ConfirmTotpCommand(Guid UserId, string Code) : ICommand<RecoveryCodesResponse>;

internal sealed class ConfirmTotpValidator : AbstractValidator<ConfirmTotpCommand>
{
    public ConfirmTotpValidator()
    {
        RuleFor(c => c.Code).NotEmpty().WithErrorCode("identity.mfa_code_required").MaximumLength(16).WithErrorCode("identity.mfa_code_invalid");
    }
}

internal sealed class ConfirmTotpHandler(UserManager<ApplicationUser> users) : ICommandHandler<ConfirmTotpCommand, RecoveryCodesResponse>
{
    public const int RecoveryCodeCount = 10;

    public async Task<Result<RecoveryCodesResponse>> Handle(ConfirmTotpCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(command.UserId.ToString()) is not { } user)
        {
            return IdentityErrors.UserNotFound;
        }

        if (user.TwoFactorEnabled)
        {
            return IdentityErrors.MfaAlreadyEnabled;
        }

        if (await users.GetAuthenticatorKeyAsync(user) is null)
        {
            return IdentityErrors.MfaSetupNotStarted;
        }

        var code = MfaCodes.Normalize(command.Code);
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            return IdentityErrors.MfaCodeInvalid;
        }

        user.EnableMfa(MfaMethod.Totp);
        await users.UpdateAsync(user);
        var recovery = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return new RecoveryCodesResponse([.. recovery!]);
    }
}

/// <summary>Liga o código por e-mail como segundo fator. O e-mail já foi confirmado no cadastro, então não há etapa extra.</summary>
public sealed record EnableEmailMfaCommand(Guid UserId) : ICommand;

internal sealed class EnableEmailMfaHandler(UserManager<ApplicationUser> users) : ICommandHandler<EnableEmailMfaCommand, Unit>
{
    public async Task<Result<Unit>> Handle(EnableEmailMfaCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(command.UserId.ToString()) is not { } user)
        {
            return IdentityErrors.UserNotFound;
        }

        if (user.TwoFactorEnabled)
        {
            return IdentityErrors.MfaAlreadyEnabled;
        }

        user.EnableMfa(MfaMethod.Email);
        await users.UpdateAsync(user);
        return Unit.Value;
    }
}

/// <summary>Pede ao Worker o código de acesso por e-mail (gerado lá, na hora do envio, como os demais e-mails de conta).</summary>
public sealed record SendSignInCodeCommand(Guid UserId) : ICommand;

internal sealed class SendSignInCodeHandler(UserManager<ApplicationUser> users, IIdentityDbContext db, IIntegrationEventPublisher events)
    : ICommandHandler<SendSignInCodeCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SendSignInCodeCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(command.UserId.ToString()) is not { MfaMethod: MfaMethod.Email } user)
        {
            return IdentityErrors.UserNotFound;
        }

        events.Publish(new AccountEmailRequestedIntegrationEvent(user.Id, AccountEmailKind.SignInCode));
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public static class MfaCodes
{
    /// <summary>Aceita o código com espaços ou hífen, como os aplicativos mostram ("123 456").</summary>
    public static string Normalize(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return code.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
    }
}

using FluentValidation;
using LexFlow.Identity.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;

namespace LexFlow.Identity.Application.Accounts;

/// <summary>
/// Política de senha (seção 7.1): mínimo de 12 caracteres e verificação contra senhas vazadas. O mínimo é aplicado
/// pelo validador do Identity; o máximo limita o custo do hash (PBKDF2) e da consulta de vazamentos.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    /// <summary>Código do <see cref="IdentityError"/> emitido pelo validador de senhas vazadas (Infrastructure).</summary>
    public const string BreachedErrorCode = "PasswordBreached";

    private static readonly IdentityErrorDescriber Describer = new();

    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode("identity.password_required")
            .MaximumLength(MaxLength).WithErrorCode("identity.password_too_long");

    /// <summary>
    /// Converte a falha do Identity no erro do módulo: senha curta e senha vazada têm códigos próprios, token inválido
    /// vira <see cref="IdentityErrors.InvalidToken"/> e as demais falhas viram <see cref="IdentityErrors.PasswordRejected"/>.
    /// </summary>
    public static Error ToError(IdentityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var codes = result.Errors.Select(e => e.Code).ToHashSet(StringComparer.Ordinal);

        if (codes.Contains(Describer.InvalidToken().Code))
        {
            return IdentityErrors.InvalidToken;
        }

        if (codes.Contains(Describer.PasswordTooShort(MinLength).Code))
        {
            return IdentityErrors.PasswordTooShort;
        }

        return codes.Contains(BreachedErrorCode) ? IdentityErrors.PasswordBreached : IdentityErrors.PasswordRejected;
    }
}

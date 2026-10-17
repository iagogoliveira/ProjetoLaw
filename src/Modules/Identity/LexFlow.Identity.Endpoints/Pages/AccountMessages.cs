using LexFlow.SharedKernel.Results;

namespace LexFlow.Identity.Endpoints.Pages;

/// <summary>Mensagens das páginas de conta, em pt-BR, a partir dos códigos estáveis de erro.</summary>
public static class AccountMessages
{
    public const string LinkInvalid = "O link é inválido ou expirou. Solicite um novo.";

    private static readonly Dictionary<string, string> ByCode = new(StringComparer.Ordinal)
    {
        ["identity.email_invalid"] = "Informe um e-mail válido.",
        ["identity.full_name_required"] = "Informe seu nome.",
        ["identity.full_name_too_long"] = "O nome pode ter até 200 caracteres.",
        ["identity.password_required"] = "Informe a senha.",
        ["identity.password_too_long"] = "A senha pode ter até 128 caracteres.",
        ["identity.user_id_required"] = LinkInvalid,
        ["identity.token_required"] = LinkInvalid,
    };

    /// <summary>Uma linha por problema; em erro de validação, uma por campo.</summary>
    public static IReadOnlyList<string> For(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error is ValidationError validation
            ? [.. validation.Errors.Select(e => ByCode.GetValueOrDefault(e.Code, e.Message)).Distinct(StringComparer.Ordinal)]
            : [ByCode.GetValueOrDefault(error.Code, error.Message)];
    }
}

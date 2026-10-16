using LexFlow.SharedKernel.Results;

namespace LexFlow.Identity.Domain;

/// <summary>Erros do módulo Identity com código estável (<c>identity.*</c>) traduzido no frontend.</summary>
public static class IdentityErrors
{
    public static readonly Error InvalidToken =
        Error.Validation("identity.invalid_token", "O link é inválido ou expirou. Solicite um novo.");

    public static readonly Error PasswordTooShort =
        Error.Validation("identity.password_too_short", "A senha deve ter pelo menos 12 caracteres.");

    public static readonly Error PasswordBreached =
        Error.Validation("identity.password_breached", "Esta senha apareceu em vazamentos conhecidos. Escolha outra.");

    public static readonly Error PasswordRejected =
        Error.Validation("identity.password_rejected", "A senha não atende à política de senhas.");

    public static readonly Error UserNotFound =
        Error.NotFound("identity.user_not_found", "Usuário não encontrado.");

    public static readonly Error SessionNotFound =
        Error.NotFound("identity.session_not_found", "Sessão não encontrada.");
}

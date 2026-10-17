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

    public static readonly Error MfaAlreadyEnabled =
        Error.Conflict("identity.mfa_already_enabled", "A verificação em duas etapas já está ativa.");

    public static readonly Error MfaCodeInvalid =
        Error.Validation("identity.mfa_code_invalid", "Código inválido ou expirado.");

    public static readonly Error MfaSetupNotStarted =
        Error.BusinessRule("identity.mfa_setup_not_started", "Comece a configuração do aplicativo autenticador antes de confirmar.");

    public static readonly Error SessionExpired =
        Error.BusinessRule("identity.session_expired", "A sessão expirou ou foi encerrada. Entre novamente.");
}

namespace LexFlow.Identity.Application.Accounts;

/// <summary>
/// Caminhos das páginas de conta no servidor de identidade (Razor, Etapa 2, PR B), usados nos links dos e-mails.
/// Os links levam <c>userId</c> e <c>token</c> (Base64Url) na query string.
/// </summary>
public static class AccountLinks
{
    public const string LoginPath = "/conta/entrar";
    public const string ConfirmEmailPath = "/conta/confirmar-email";
    public const string ResetPasswordPath = "/conta/redefinir-senha";
    public const string ForgotPasswordPath = "/conta/esqueci-a-senha";
}

using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Mfa;
using LexFlow.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexFlow.Identity.Endpoints.Pages.Conta;

/// <summary>
/// Segundo fator do login: código do aplicativo autenticador, código enviado por e-mail ou, para quem usa TOTP, um
/// código de recuperação (de uso único). Erros contam para o bloqueio por tentativas, como a senha.
/// </summary>
public sealed class SegundoFatorModel(
    SignInManager<ApplicationUser> signIn,
    ICommandHandler<SendSignInCodeCommand, Unit> sendCode) : PageModel
{
    public const string CodeInvalid = "Código inválido ou expirado.";

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public bool UseRecoveryCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public MfaMethod Method { get; private set; }

    public string? Message { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool SignedInWithoutReturn { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is not { } user)
        {
            return RedirectToPage("/Conta/Entrar", new { ReturnUrl });
        }

        Method = user.MfaMethod;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is not { } user)
        {
            return RedirectToPage("/Conta/Entrar", new { ReturnUrl });
        }

        Method = user.MfaMethod;
        // Códigos de recuperação têm hífen ("ABCDE-FGHIJ") e são comparados como foram gerados: só tira espaços.
        var code = MfaCodes.Normalize(Code);
        var result = UseRecoveryCode && Method == MfaMethod.Totp
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(Code.Trim())
            : Method == MfaMethod.Email
                ? await signIn.TwoFactorSignInAsync(TokenOptions.DefaultEmailProvider, code, isPersistent: false, rememberClient: false)
                : await signIn.TwoFactorAuthenticatorSignInAsync(code, isPersistent: false, rememberClient: false);

        Code = string.Empty;
        if (result.Succeeded)
        {
            if (ReturnUrl is { Length: > 0 } returnUrl && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            SignedInWithoutReturn = true;
            return Page();
        }

        ErrorMessage = result.IsLockedOut ? EntrarModel.LockedOut : CodeInvalid;
        return Page();
    }

    /// <summary>Novo código por e-mail (o anterior continua valendo até expirar).</summary>
    public async Task<IActionResult> OnPostResendAsync()
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is not { } user)
        {
            return RedirectToPage("/Conta/Entrar", new { ReturnUrl });
        }

        Method = user.MfaMethod;
        await sendCode.Handle(new SendSignInCodeCommand(user.Id), HttpContext.RequestAborted);
        Message = "Enviamos um novo código.";
        return Page();
    }
}

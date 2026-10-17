using LexFlow.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexFlow.Identity.Endpoints.Pages.Conta;

/// <summary>
/// Login com e-mail e senha. A 6ª tentativa errada seguida bloqueia a conta por 15 minutos. Em sucesso, volta ao
/// endpoint de autorização que pediu o login (só endereços locais, para não virar redirecionamento aberto).
/// </summary>
public sealed class EntrarModel(SignInManager<ApplicationUser> signIn) : PageModel
{
    public const string InvalidCredentials = "E-mail ou senha inválidos.";
    public const string LockedOut = "Conta bloqueada por 15 minutos depois de várias tentativas erradas. Tente mais tarde ou redefina a senha.";
    public const string EmailNotConfirmed = "Confirme seu e-mail antes de entrar. Procure a mensagem que enviamos.";

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public bool SignedInWithoutReturn { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await signIn.PasswordSignInAsync(Email.Trim().ToLowerInvariant(), Password, isPersistent: false, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (ReturnUrl is { Length: > 0 } returnUrl && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            SignedInWithoutReturn = true;
            return Page();
        }

        ErrorMessage = result.IsLockedOut ? LockedOut : result.IsNotAllowed ? EmailNotConfirmed : InvalidCredentials;
        Password = string.Empty;
        return Page();
    }
}

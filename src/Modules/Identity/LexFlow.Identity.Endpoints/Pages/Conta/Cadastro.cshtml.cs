using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexFlow.Identity.Endpoints.Pages.Conta;

/// <summary>Cadastro aberto. A mensagem de sucesso é a mesma se o e-mail já tem conta (ADR 0013).</summary>
public sealed class CadastroModel(ICommandHandler<RegisterUserCommand, Unit> register) : PageModel
{
    [BindProperty]
    public string FullName { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public IReadOnlyList<string> Errors { get; private set; } = [];

    public bool Registered { get; private set; }

    /// <summary>Cadastro a partir de um convite: a página do convite manda o e-mail convidado já preenchido.</summary>
    public void OnGet(string? email) => Email = email?.Trim() ?? string.Empty;

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await register.Handle(new RegisterUserCommand(Email, FullName, Password), cancellationToken);
        Password = string.Empty;
        if (result.IsFailure)
        {
            Errors = AccountMessages.For(result.Error);
            return Page();
        }

        Registered = true;
        return Page();
    }
}

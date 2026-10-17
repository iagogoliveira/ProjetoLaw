using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexFlow.Identity.Endpoints.Pages.Conta;

/// <summary>Pede o link de redefinição. A resposta é a mesma para qualquer e-mail válido (ADR 0013).</summary>
public sealed class EsqueciASenhaModel(ICommandHandler<ForgotPasswordCommand, Unit> forgot) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public IReadOnlyList<string> Errors { get; private set; } = [];

    public bool Requested { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await forgot.Handle(new ForgotPasswordCommand(Email), cancellationToken);
        if (result.IsFailure)
        {
            Errors = AccountMessages.For(result.Error);
            return Page();
        }

        Requested = true;
        return Page();
    }
}

using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexFlow.Identity.Endpoints.Pages.Conta;

/// <summary>Nova senha a partir do link do e-mail. Em sucesso, todas as sessões abertas são encerradas.</summary>
public sealed class RedefinirSenhaModel(ICommandHandler<ResetPasswordCommand, Unit> reset) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    public IReadOnlyList<string> Errors { get; private set; } = [];

    public bool Reset { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await reset.Handle(new ResetPasswordCommand(UserId, Token, NewPassword), cancellationToken);
        NewPassword = string.Empty;
        if (result.IsFailure)
        {
            Errors = AccountMessages.For(result.Error);
            return Page();
        }

        Reset = true;
        return Page();
    }
}

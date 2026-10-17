using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Accounts;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LexFlow.Identity.Endpoints.Pages.Conta;

/// <summary>Link do e-mail de confirmação. Confirmar de novo não tem efeito, então abrir o link mais de uma vez é seguro.</summary>
public sealed class ConfirmarEmailModel(ICommandHandler<ConfirmEmailCommand, Unit> confirm) : PageModel
{
    public bool Confirmed { get; private set; }

    public async Task OnGetAsync(Guid userId, string? token, CancellationToken cancellationToken) =>
        Confirmed = (await confirm.Handle(new ConfirmEmailCommand(userId, token ?? string.Empty), cancellationToken)).IsSuccess;
}

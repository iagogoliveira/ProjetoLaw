using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Application.Accounts;
using LexFlow.Identity.Contracts;
using LexFlow.Identity.Domain.Users;
using Scriban;

namespace LexFlow.Identity.Infrastructure.Emails;

/// <summary>
/// Monta os e-mails de conta a partir dos templates Scriban embutidos (<c>EmailTemplates/*.sbn</c>, em pt-BR).
/// Os templates escapam todo valor com <c>html.escape</c>: o nome do usuário vem do cadastro e não é confiável.
/// </summary>
public sealed class AccountEmailComposer(IdentitySettings settings)
{
    private static readonly Template Layout = Load("layout");
    private static readonly Template ConfirmEmail = Load("confirm-email");
    private static readonly Template ResetPassword = Load("reset-password");
    private static readonly Template AlreadyRegistered = Load("already-registered");

    /// <summary><paramref name="token"/> é o token do Identity, ainda sem codificação; obrigatório para confirmação e redefinição.</summary>
    public EmailMessage Compose(AccountEmailKind kind, ApplicationUser user, string? token)
    {
        ArgumentNullException.ThrowIfNull(user);
        var address = user.Email ?? throw new ArgumentException("Usuário sem e-mail.", nameof(user));

        var (subject, body, link) = kind switch
        {
            AccountEmailKind.ConfirmEmail => ("Confirme seu e-mail no LexFlow", ConfirmEmail, TokenLink(AccountLinks.ConfirmEmailPath, user, token)),
            AccountEmailKind.ResetPassword => ("Redefinição de senha do LexFlow", ResetPassword, TokenLink(AccountLinks.ResetPasswordPath, user, token)),
            AccountEmailKind.AlreadyRegistered => ("Você já tem uma conta no LexFlow", AlreadyRegistered, new Uri(settings.PublicUrl, AccountLinks.ForgotPasswordPath)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de e-mail desconhecido."),
        };

        var content = body.Render(new { name = user.FullName, link = link.AbsoluteUri });
        var html = Layout.Render(new { subject, content });
        return new EmailMessage([address], subject, html);
    }

    private Uri TokenLink(string path, ApplicationUser user, string? token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return new Uri(settings.PublicUrl, $"{path}?userId={user.Id:D}&token={AccountTokens.Encode(token)}");
    }

    private static Template Load(string name)
    {
        var resource = $"{typeof(AccountEmailComposer).Assembly.GetName().Name}.EmailTemplates.{name}.sbn";
        using var stream = typeof(AccountEmailComposer).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Template de e-mail '{resource}' não encontrado.");
        using var reader = new StreamReader(stream);
        var template = Template.Parse(reader.ReadToEnd(), name);
        return template.HasErrors
            ? throw new InvalidOperationException($"Template de e-mail '{name}' inválido: {string.Join("; ", template.Messages)}")
            : template;
    }
}

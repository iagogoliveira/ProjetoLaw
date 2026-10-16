using LexFlow.Application.Abstractions.Integrations;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LexFlow.Infrastructure.Common.Integrations;

/// <summary>Configuração SMTP (seção <c>Email</c>). Credenciais vêm de user-secrets/variáveis de ambiente, nunca do código.</summary>
public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1025;

    /// <summary>STARTTLS obrigatório em produção; o Mailpit local não usa TLS.</summary>
    public bool UseStartTls { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public string FromAddress { get; init; } = "nao-responda@lexflow.local";

    public string FromName { get; init; } = "LexFlow";
}

internal sealed class SmtpEmailSender(EmailSettings settings) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.AddRange(message.To.Select(MailboxAddress.Parse));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}

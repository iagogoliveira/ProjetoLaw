using System.Globalization;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Contracts;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Organization.Application.Invitations;
using LexFlow.Organization.Contracts;
using LexFlow.Organization.Domain.Invitations;
using LexFlow.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scriban;

namespace LexFlow.Organization.Infrastructure.Invitations;

/// <summary>
/// Monta o e-mail do convite a partir dos templates Scriban embutidos (<c>Invitations/EmailTemplates/*.sbn</c>, em
/// pt-BR). Todo valor passa por <c>html.escape</c>: nomes de pessoa e de escritório vêm de cadastro e não são confiáveis.
/// </summary>
public sealed class InvitationEmailComposer(OrganizationSettings settings, IClock clock)
{
    public const string AcceptPath = "convite";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Template Layout = Load("layout");
    private static readonly Template Body = Load("invitation");

    public Uri LinkFor(string token) => new(settings.AppUrl, $"{AcceptPath}?token={Uri.EscapeDataString(token)}");

    public EmailMessage Compose(string email, string organization, string inviter, DateTimeOffset expiresAt, string token)
    {
        var subject = $"Convite para o escritório {organization} no LexFlow";
        var expires = TimeZoneInfo.ConvertTime(expiresAt, clock.LegalTimeZone).ToString("dd/MM/yyyy 'às' HH:mm", PtBr);
        var content = Body.Render(new { inviter, organization, email, expires, link = LinkFor(token).AbsoluteUri });
        return new EmailMessage([email], subject, Layout.Render(new { subject, content }));
    }

    private static Template Load(string name)
    {
        var resource = $"{typeof(InvitationEmailComposer).Assembly.GetName().Name}.Invitations.EmailTemplates.{name}.sbn";
        using var stream = typeof(InvitationEmailComposer).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Template de e-mail '{resource}' não encontrado.");
        using var reader = new StreamReader(stream);
        var template = Template.Parse(reader.ReadToEnd(), name);
        return template.HasErrors
            ? throw new InvalidOperationException($"Template de e-mail '{name}' inválido: {string.Join("; ", template.Messages)}")
            : template;
    }
}

/// <summary>
/// Envia, no Worker, o e-mail do convite. O token nasce aqui, na hora do envio, e só o hash é gravado, no mesmo commit
/// do registro de processamento. Se esse commit falhar depois do envio, a reentrega manda outro e-mail com outro token
/// (o primeiro nunca chegou a valer). Convite já aceito, cancelado ou vencido não gera e-mail.
/// </summary>
public sealed class InvitationEmailConsumer(
    OrganizationDbContext db,
    IClock clock,
    IUserDirectory users,
    InvitationEmailComposer composer,
    IEmailSender email)
    : IntegrationEventConsumer<InvitationEmailRequestedIntegrationEvent, OrganizationDbContext>(db, clock)
{
    private readonly IClock _clock = clock;

    protected override async Task ConsumeAsync(InvitationEmailRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var invitation = await Db.Invitations.AsNoTracking().SingleOrDefaultAsync(i => i.Id == integrationEvent.InvitationId, cancellationToken);
        if (invitation is null || !invitation.IsOpen(_clock.UtcNow))
        {
            return;
        }

        var organization = await Db.Organizations.Where(o => o.Id == invitation.OrganizationId).Select(o => o.Name).SingleAsync(cancellationToken);
        var inviter = (await users.FindAsync(invitation.InvitedBy, cancellationToken))?.FullName ?? organization;

        var token = InvitationTokens.NewToken();
        Db.InvitationTokens.RemoveRange(await Db.InvitationTokens.Where(t => t.InvitationId == invitation.Id).ToListAsync(cancellationToken));
        Db.InvitationTokens.Add(new InvitationToken(InvitationTokens.Hash(token), invitation.Id, invitation.OrganizationId, invitation.Email, invitation.ExpiresAt));

        await email.SendAsync(composer.Compose(invitation.Email, organization, inviter, invitation.ExpiresAt, token), cancellationToken);
    }
}

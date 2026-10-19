using System.Globalization;
using System.Net;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Contracts;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Domain.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Publications.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Infrastructure.Notifications;

public sealed class PublicationNotificationConsumer(PlatformDbContext db, IClock clock)
    : IntegrationEventConsumer<PublicationCapturedIntegrationEvent, PlatformDbContext>(db, clock)
{
    protected override async Task ConsumeAsync(PublicationCapturedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var key = "publication:" + integrationEvent.PublicationId.ToString("N");
        if (!await Db.Notifications.AnyAsync(n => n.Key == key, cancellationToken))
        {
            Db.Notifications.Add(Notification.Create(key, "publications.received", integrationEvent.PublicationId, integrationEvent.CaseId, integrationEvent.OccurredAt));
        }
    }
}

public sealed class CaptureNotificationConsumer(PlatformDbContext db, IClock clock)
    : IntegrationEventConsumer<CaptureMissingIntegrationEvent, PlatformDbContext>(db, clock)
{
    protected override async Task ConsumeAsync(CaptureMissingIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var key = "capture:" + integrationEvent.AlertId.ToString("N");
        if (!await Db.Notifications.AnyAsync(n => n.Key == key, cancellationToken))
        {
            Db.Notifications.Add(Notification.Create(key, "publications.capture_missing", integrationEvent.AlertId, null, integrationEvent.OccurredAt));
        }
    }
}

public sealed class CaptureEmailConsumer(PlatformDbContext db, IClock clock, IOrganizationAccessDirectory directory, IUserDirectory users,
    ICaptureAlertDirectory alerts, IEmailSender email)
    : IntegrationEventConsumer<CaptureMissingIntegrationEvent, PlatformDbContext>(db, clock)
{
    protected override async Task ConsumeAsync(CaptureMissingIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var alert = await alerts.FindAsync(integrationEvent.AlertId, cancellationToken);
        if (alert is null || !alert.Active || alert.Resolved) { return; }
        var recipients = new List<Guid>();
        foreach (var user in await directory.UserIdsAsync(cancellationToken))
        {
            var access = await directory.GetAsync(user, cancellationToken);
            if (access is not null && (Has(access, "settings.integrations.manage") || Has(access, "publications.triage")))
            {
                recipients.Add(user);
            }
        }
        foreach (var user in await users.GetManyAsync(recipients, cancellationToken))
        {
            if (!user.EmailConfirmed) { continue; }
            var day = alert.Day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            var body = $"A captura de {day} da fonte {alert.SourceLabel} ainda não foi concluída. Confira o monitoramento de publicações.";
            await email.SendAsync(new EmailMessage([user.Email], "LexFlow — captura de publicações pendente", "<p>" + WebUtility.HtmlEncode(body) + "</p>", body), cancellationToken);
        }
    }

    private static bool Has(OfficeUserAccess access, string permission) =>
        access.Permissions.TryGetValue(permission, out var grants) && grants.Any(s => s.Scope == OfficeAccessScope.Organization);
}

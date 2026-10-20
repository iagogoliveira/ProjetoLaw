using System.Diagnostics.Metrics;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Platform.Domain.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Scheduling.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Infrastructure.Notifications;

public sealed class DeadlineNotificationConsumer(
    PlatformDbContext db,
    IClock clock,
    IDeadlineAlertDirectory alerts)
    : IntegrationEventConsumer<DeadlineAlertIntegrationEvent, PlatformDbContext>(db, clock),
        IIntegrationEventHandler<DeadlineAlertIntegrationEvent>
{
    private const string DeadlineMissed = "scheduling.deadline_missed";
    private const string CalendarChanged = "scheduling.calendar_changed";
    private static readonly Meter Meter = new("LexFlow.Scheduling");
    private static readonly Counter<long> MissedDeadlines = Meter.CreateCounter<long>("lexflow_deadlines_missed_total");

    // Wolverine descobre o método público; a reimplementação da interface também usa este wrapper.
    public new async Task Handle(
        DeadlineAlertIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        Guid? organizationId = integrationEvent.OrganizationId == Guid.Empty ? null : integrationEvent.OrganizationId;
        using var scope = AmbientContext.Begin(organizationId, integrationEvent.CorrelationId);
        var key = Key(integrationEvent.ActivityId);
        var notificationAlreadyExists = await Db.Notifications.AnyAsync(n => n.Key == key, cancellationToken);

        await base.Handle(integrationEvent, cancellationToken);

        if (!notificationAlreadyExists
            && string.Equals(integrationEvent.Kind, DeadlineMissed, StringComparison.Ordinal)
            && await Db.Notifications.AnyAsync(n => n.Key == key, cancellationToken))
        {
            MissedDeadlines.Add(1);
        }
    }

    protected override async Task ConsumeAsync(DeadlineAlertIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (!IsSupported(integrationEvent.Kind))
        {
            return;
        }

        var alert = await alerts.FindAsync(integrationEvent.ActivityId, cancellationToken);
        if (alert is null || alert.DeadlineId != integrationEvent.DeadlineId || alert.CaseId != integrationEvent.CaseId
            || !string.Equals(alert.ActivityKind, integrationEvent.Kind, StringComparison.Ordinal))
        {
            return;
        }

        var key = Key(integrationEvent.ActivityId);
        if (!await Db.Notifications.AnyAsync(notification => notification.Key == key, cancellationToken))
        {
            Db.Notifications.Add(Notification.Create(key, integrationEvent.Kind, integrationEvent.ActivityId,
                integrationEvent.CaseId, integrationEvent.OccurredAt));
        }
    }

    private static string Key(Guid activityId) => $"deadline:{activityId:N}";

    private static bool IsSupported(string kind) =>
        string.Equals(kind, DeadlineMissed, StringComparison.Ordinal)
        || string.Equals(kind, CalendarChanged, StringComparison.Ordinal);
}

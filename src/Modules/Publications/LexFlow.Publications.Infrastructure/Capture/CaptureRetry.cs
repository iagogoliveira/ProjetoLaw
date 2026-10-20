using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;

namespace LexFlow.Publications.Infrastructure.Capture;

internal sealed class CaptureRetryAudit(PublicationsDbContext db, IClock clock, ICurrentUser user,
    ITenantContext tenant, IRequestContext request) : ICaptureRetryAudit
{
    public void Add(Guid sourceId, Guid requestId, DateOnly day) => db.Set<AuditLogEntry>().Add(new AuditLogEntry
    {
        Id = requestId,
        OrganizationId = tenant.OrganizationId,
        UserId = user.UserId,
        Action = "CaptureRetryRequested",
        EntityType = nameof(CaptureSource),
        EntityId = sourceId.ToString(),
        Changes = JsonSerializer.Serialize(new { day, requestId }),
        Ip = request.IpAddress,
        UserAgent = request.UserAgent,
        CorrelationId = request.CorrelationId,
        OccurredAt = clock.UtcNow,
    });
}

public sealed class CaptureRetryConsumer(PublicationsDbContext db, IClock clock, IJobScheduler jobs)
    : IntegrationEventConsumer<CaptureRetryRequestedIntegrationEvent, PublicationsDbContext>(db, clock)
{
    protected override Task ConsumeAsync(CaptureRetryRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        jobs.Enqueue<CaptureSourceJob>(job => job.RunAsync(integrationEvent.SourceId, integrationEvent.Day, true));
        return Task.CompletedTask;
    }
}

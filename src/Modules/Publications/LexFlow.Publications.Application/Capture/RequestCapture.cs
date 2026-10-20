using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Capture;

public sealed record RequestCaptureCommand(Guid SourceId, DateOnly Day) : ICommand<CaptureRetryResponse>;
public sealed record CaptureRetryResponse(Guid RequestId, DateOnly Day);
public interface ICaptureRetryAudit
{
    void Add(Guid sourceId, Guid requestId, DateOnly day);
}

internal sealed class RequestCaptureHandler(IPublicationsDbContext db, ICurrentAccess access, IClock clock,
    IIdGenerator ids, ICaptureLocks locks, ICaptureRetryAudit audit, IIntegrationEventPublisher events)
    : ICommandHandler<RequestCaptureCommand, CaptureRetryResponse>
{
    public async Task<Result<CaptureRetryResponse>> Handle(RequestCaptureCommand command, CancellationToken cancellationToken)
    {
        if (!(await access.ScopesForAsync("settings.integrations.manage", cancellationToken)).Any(s => s.Scope == AccessScope.Organization))
        { return Error.Forbidden("publications.capture_access_denied", "A captura manual exige gestão de integrações no escritório."); }
        await locks.ControlAsync(cancellationToken);
        var source = await db.Sources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == command.SourceId, cancellationToken);
        if (source is null) { return PublicationErrors.SourceNotFound; }
        if (!source.IsActive) { return Error.Conflict("publications.source_inactive", "Ative a fonte antes de solicitar captura."); }
        if (command.Day == default || !CaptureSchedule.CanCapture(source, command.Day, clock.UtcNow, clock.LegalTimeZone, force: true))
        { return Error.Validation("publications.capture_day_invalid", "Informe um dia entre a última ativação e hoje."); }
        var requestId = ids.NewId();
        audit.Add(source.Id, requestId, command.Day);
        events.Publish(new CaptureRetryRequestedIntegrationEvent(source.Id, command.Day, requestId));
        await db.SaveChangesAsync(cancellationToken);
        return new CaptureRetryResponse(requestId, command.Day);
    }
}

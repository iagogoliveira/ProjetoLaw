using LexFlow.SharedKernel.Messaging;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Contracts;

public sealed record CaptureQuery(Guid SourceId, string Kind, string Value, DateOnly Day);
public sealed record CapturedPublication(string Journal, DateOnly AvailableOn, DateOnly PublishedOn, string Text);
public sealed record CapturedMovement(string ExternalId, DateTimeOffset OccurredAt, string Description);
public interface IPublicationProvider
{
    Task<Result<IReadOnlyList<CapturedPublication>>> FetchAsync(CaptureQuery query, CancellationToken cancellationToken);
}
public interface IMovementProvider
{
    Task<Result<IReadOnlyList<CapturedMovement>>> FetchAsync(string cnj, DateOnly since, CancellationToken cancellationToken);
}
public sealed record PublicationCapturedIntegrationEvent(Guid PublicationId, Guid? CaseId) : IntegrationEvent;
public sealed record CaptureMissingIntegrationEvent(Guid AlertId, Guid SourceId, DateOnly Day) : IntegrationEvent;

public sealed record SourceActivatedIntegrationEvent(Guid SourceId) : IntegrationEvent;

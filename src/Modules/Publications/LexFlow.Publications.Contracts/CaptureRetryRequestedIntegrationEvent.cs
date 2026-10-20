using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Publications.Contracts;

public sealed record CaptureRetryRequestedIntegrationEvent(Guid SourceId, DateOnly Day, Guid RequestId) : IntegrationEvent;

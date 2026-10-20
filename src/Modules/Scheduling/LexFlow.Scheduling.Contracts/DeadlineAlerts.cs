using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Scheduling.Contracts;

public sealed record DeadlineAlertIntegrationEvent(Guid ActivityId, Guid DeadlineId, Guid CaseId, string Kind) : IntegrationEvent;

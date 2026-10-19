using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Contracts;

public sealed record DeadlineDraft(Guid CaseId, Guid PublicationId, string Title, DateOnly StartOn,
    DateOnly InternalDueOn, DateOnly FatalDueOn, Guid AssigneeUserId, bool Confirmed);
public sealed record TaskDraft(Guid CaseId, Guid PublicationId, string Title, DateOnly DueOn, Guid AssigneeUserId, bool Confirmed);

/// <summary>Entrada da triagem; o chamador participa da mesma transação local (ADR 0035).</summary>
public interface ISchedulingIntake
{
    Task<Result<Guid>> CreateDeadlineAsync(DeadlineDraft draft, CancellationToken cancellationToken);
    Task<Result<Guid>> CreateTaskAsync(TaskDraft draft, CancellationToken cancellationToken);
}

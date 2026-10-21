namespace LexFlow.Scheduling.Contracts;

public sealed record FatalDeadlineReminder(Guid Id, Guid CaseId);

/// <summary>Dispatcher interno de alertas do dia; visibilidade é revalidada no feed por usuário.</summary>
public interface IFatalDeadlineReminders
{
    Task<IReadOnlyList<FatalDeadlineReminder>> DueAsync(DateOnly day, CancellationToken cancellationToken);
}

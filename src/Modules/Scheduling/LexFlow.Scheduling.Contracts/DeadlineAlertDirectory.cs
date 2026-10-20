namespace LexFlow.Scheduling.Contracts;

/// <summary>Dados mínimos de um alerta de prazo, ligados à atividade que o originou.</summary>
public sealed record DeadlineAlertDetails(
    Guid ActivityId,
    Guid DeadlineId,
    Guid CaseId,
    string ActivityKind,
    Guid AssigneeUserId,
    Guid? ReviewedBy);

/// <summary>Consulta o prazo que originou uma notificação e aplica a mesma regra de visibilidade usada no feed.</summary>
public interface IDeadlineAlertDirectory
{
    Task<DeadlineAlertDetails?> FindAsync(Guid activityId, CancellationToken cancellationToken);

}

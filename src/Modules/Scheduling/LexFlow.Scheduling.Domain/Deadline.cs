using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain;

public enum WorkItemStatus { Pending }

/// <summary>Núcleo antecipado da Etapa 7: datas humanas, sem calculadora nem baixa de cumprimento.</summary>
public sealed class Deadline : Entity<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    private Deadline() { }
    public Guid OrganizationId { get; private set; }
    public Guid LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public Guid PublicationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly StartOn { get; private set; }
    public DateOnly InternalDueOn { get; private set; }
    public DateOnly FatalDueOn { get; private set; }
    public Guid AssigneeUserId { get; private set; }
    public Guid ConfirmedBy { get; private set; }
    public DateTimeOffset ConfirmedAt { get; private set; }
    public WorkItemStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Result<Deadline> Create(Guid caseId, Guid publicationId, string title, DateOnly startOn,
        DateOnly internalDueOn, DateOnly fatalDueOn, Guid assignee, bool confirmed, Guid userId, DateTimeOffset now)
    {
        if (!WorkItemRules.Valid(caseId, publicationId, title, assignee, confirmed, userId)
            || startOn == default || internalDueOn < startOn || fatalDueOn < internalDueOn)
        {
            return SchedulingErrors.InvalidDeadline;
        }

        return new Deadline
        {
            Id = Guid.CreateVersion7(),
            LegalCaseId = caseId,
            PublicationId = publicationId,
            Title = title.Trim(),
            StartOn = startOn,
            InternalDueOn = internalDueOn,
            FatalDueOn = fatalDueOn,
            AssigneeUserId = assignee,
            ConfirmedBy = userId,
            ConfirmedAt = now.ToUniversalTime(),
        };
    }
}

internal static class WorkItemRules
{
    public static bool Valid(Guid caseId, Guid publicationId, string title, Guid assignee, bool confirmed, Guid userId) =>
        caseId != Guid.Empty && publicationId != Guid.Empty && !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= 300
        && assignee != Guid.Empty && confirmed && userId != Guid.Empty;
}

public static class SchedulingErrors
{
    public static readonly Error InvalidDeadline = Error.Validation("scheduling.deadline_invalid", "Confirme responsável, título e datas válidas do prazo.");
    public static readonly Error InvalidTask = Error.Validation("scheduling.task_invalid", "Confirme responsável, título e data válida da tarefa.");
    public static readonly Error AssigneeUnavailable = Error.Validation("scheduling.assignee_unavailable", "Responsável inativo ou sem acesso ao processo.");
    public static readonly Error CaseUnavailable = Error.BusinessRule("scheduling.case_unavailable", "Processo inexistente ou encerrado.");
    public static readonly Error InvalidCalculation = Error.Validation("scheduling.calculation_invalid", "Informe o início, de 1 a 365 dias e uma data interna de até 30 dias úteis antes do vencimento.");
    public static readonly Error CalendarOutOfRange = Error.BusinessRule("scheduling.calendar_out_of_range", "O prazo ultrapassa o período do calendário disponível.");
    public static readonly Error InvalidCalendarEntry = Error.Validation("scheduling.calendar_entry_invalid", "Informe nome, período válido e a abrangência do dia sem expediente.");
    public static readonly Error CourtNotFound = Error.Validation("scheduling.court_not_found", "Tribunal não encontrado.");
    public static readonly Error CalendarEntryNotFound = Error.NotFound("scheduling.calendar_entry_not_found", "Dia sem expediente não encontrado.");
}

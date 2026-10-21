using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain;

public static partial class SchedulingErrors
{
    public static readonly Error InvalidDeadlineList = Error.Validation("scheduling.deadline_list_invalid", "Informe intervalo de até 366 dias, página de 1 a 100 itens e cursor válido.");
    public static readonly Error TaskNotFound = Error.NotFound("scheduling.task_not_found", "Tarefa não encontrada.");
    public static readonly Error TaskTransitionInvalid = Error.Conflict("scheduling.task_transition_invalid", "Transição ou alteração indisponível neste estado da tarefa.");
    public static readonly Error TaskUnresolved = Error.BusinessRule("scheduling.task_unresolved", "Conclua o checklist e resolva as subtarefas antes de concluir a tarefa.");
    public static readonly Error InvalidChecklist = Error.Validation("scheduling.checklist_invalid", "Informe título de até 300 caracteres; limite de 100 itens.");
    public static readonly Error ChecklistNotFound = Error.NotFound("scheduling.checklist_not_found", "Item de checklist não encontrado.");
    public static readonly Error InvalidComment = Error.Validation("scheduling.comment_invalid", "Informe comentário de até 4000 caracteres.");
    public static readonly Error InvalidSubtask = Error.BusinessRule("scheduling.subtask_invalid", "Subtarefa exige tarefa principal aberta, do mesmo processo e com apenas um nível.");
    public static readonly Error InvalidEvent = Error.Validation("scheduling.event_invalid", "Informe título, responsável e intervalo válido do compromisso.");
    public static readonly Error EventNotFound = Error.NotFound("scheduling.event_not_found", "Compromisso não encontrado.");
    public static readonly Error EventClosed = Error.Conflict("scheduling.event_closed", "Compromisso cancelado não pode ser alterado.");
    public static readonly Error InvalidAgenda = Error.Validation("scheduling.agenda_invalid", "Informe intervalo de até 366 dias e página de 1 a 100 itens.");
    public static readonly Error InvalidBatch = Error.Validation("scheduling.batch_invalid", "Informe de 1 a 1000 itens distintos e responsável válido.");
}

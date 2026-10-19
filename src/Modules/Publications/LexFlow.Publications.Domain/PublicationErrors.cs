using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Domain;

public static class PublicationErrors
{
    public static readonly Error AlreadyTriaged = Error.Conflict("publications.already_triaged", "A publicação já foi triada.");
    public static readonly Error InvalidTriage = Error.Validation("publications.triage_invalid", "Confirme classificação, ação e justificativa da triagem.");
    public static readonly Error CaseRequired = Error.Validation("publications.case_required", "Vincule um processo antes de concluir a triagem.");
    public static readonly Error AssigneeUnavailable = Error.Validation("publications.assignee_unavailable", "Responsável inativo ou sem acesso à publicação e à triagem.");
    public static readonly Error CloseDenied = Error.Forbidden("publications.close_access_denied", "O fechamento exige permissão no escritório e alcance de todas as publicações.");
    public static readonly Error InvalidCloseDay = Error.Validation("publications.close_day_invalid", "Informe uma data válida até hoje.");
    public static readonly Error PendingTriage = Error.Conflict("publications.close_pending_triage", "Há publicações pendentes até a data escolhida.");
    public static readonly Error IncompleteCapture = Error.Conflict("publications.close_incomplete_capture", "Há capturas esperadas sem conclusão bem-sucedida.");
    public static readonly Error InvalidSource = Error.Validation("publications.source_invalid", "Fonte de captura inválida.");
    public static readonly Error SourceNotFound = Error.NotFound("publications.source_not_found", "Fonte de captura não encontrada.");
    public static readonly Error NotFound = Error.NotFound("publications.not_found", "Publicação não encontrada.");
    public static readonly Error InvalidPublication = Error.Validation("publications.invalid", "Dados da publicação inválidos.");
    public static readonly Error InvalidMovement = Error.Validation("publications.movement_invalid", "Andamento inválido.");
    public static readonly Error InvalidRun = Error.Validation("publications.capture_run_invalid", "Execução de captura inválida.");
}

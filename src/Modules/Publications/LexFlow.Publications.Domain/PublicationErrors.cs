using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Domain;

public static class PublicationErrors
{
    public static readonly Error InvalidSource = Error.Validation("publications.source_invalid", "Fonte de captura inválida.");
    public static readonly Error SourceNotFound = Error.NotFound("publications.source_not_found", "Fonte de captura não encontrada.");
    public static readonly Error NotFound = Error.NotFound("publications.not_found", "Publicação não encontrada.");
    public static readonly Error InvalidPublication = Error.Validation("publications.invalid", "Dados da publicação inválidos.");
    public static readonly Error InvalidMovement = Error.Validation("publications.movement_invalid", "Andamento inválido.");
    public static readonly Error InvalidRun = Error.Validation("publications.capture_run_invalid", "Execução de captura inválida.");
}

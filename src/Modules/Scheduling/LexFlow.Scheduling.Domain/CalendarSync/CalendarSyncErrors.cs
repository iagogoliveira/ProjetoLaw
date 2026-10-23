using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain.CalendarSync;

public static class CalendarSyncErrors
{
    public static readonly Error InvalidConnection = Error.Validation("scheduling.calendar_connection_invalid", "Informe provedor e período de até 366 dias.");
    public static readonly Error NotFound = Error.NotFound("scheduling.calendar_connection_not_found", "Conexão de calendário não encontrada.");
    public static readonly Error ProviderUnavailable = Error.BusinessRule("scheduling.calendar_provider_unavailable", "Provedor indisponível; os adaptadores reais dependem da configuração OAuth.");
    public static readonly Error InvalidRemote = Error.Validation("scheduling.calendar_remote_invalid", "O compromisso externo não pode ser representado nesta agenda.");
    public static readonly Error RemoteConflict = Error.Conflict("scheduling.calendar_remote_conflict", "O item externo mudou; sincronize novamente antes de decidir.");
    public static readonly Error PendingNotFound = Error.NotFound("scheduling.calendar_pending_not_found", "Pendência de calendário não encontrada.");
    public static readonly Error StaleDecision = Error.Conflict("scheduling.calendar_decision_stale", "O compromisso mudou; sincronize novamente antes de decidir.");
    public static readonly Error Inactive = Error.BusinessRule("scheduling.calendar_connection_inactive", "A conexão está desativada.");
    public static readonly Error SyncLimit = Error.BusinessRule("scheduling.calendar_sync_limit", "O período ultrapassa o limite da sincronização; reduza a janela e tente novamente.");
    public static readonly Error AccessUnavailable = Error.BusinessRule("scheduling.calendar_access_unavailable", "O usuário não possui mais o acesso necessário.");
}

using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Contracts;

/// <summary>Consultas e autorização de processo para outros módulos, sem compartilhar DbContext.</summary>
public interface ICaseDirectory
{
    Task<bool> IsOpenAsync(Guid caseId, CancellationToken cancellationToken);
    Task<CaseReadScope> ScopeForUserAsync(Guid userId, string permission, CancellationToken cancellationToken);
    Task<Result> AuthorizeAsync(Guid caseId, string permission, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> VisibleIdsAsync(string permission, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> InaccessibleCnjsAsync(string permission, CancellationToken cancellationToken);
    /// <summary>Busca no tenant para captura interna; não autoriza leitura do processo.</summary>
    Task<Guid?> FindByCnjAsync(string cnj, CancellationToken cancellationToken);
    /// <summary>Somente para jobs internos: processos judiciais ativos do tenant, por chave.</summary>
    Task<IReadOnlyList<MonitoredCase>> MonitoredAsync(Guid? after, int limit, CancellationToken cancellationToken);
    Task<MonitoredCase?> MonitoredCaseAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record MonitoredCase(Guid Id, string Cnj);

public sealed record CaseReadScope(IReadOnlyCollection<Guid> VisibleIds, IReadOnlyCollection<string> HiddenCnjs);

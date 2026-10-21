namespace LexFlow.Litigation.Contracts;

public sealed record ActiveControlCase(Guid Id, string Folder, DateTimeOffset CreatedAt);

/// <summary>Projeção mínima de processos ativos, com leitura, escopo de controladoria e sigilo verificados.</summary>
public interface ICaseControlDirectory
{
    Task<IReadOnlyList<ActiveControlCase>> ActiveAsync(Guid userId, bool multiFactorAuthenticated, CancellationToken cancellationToken);
}

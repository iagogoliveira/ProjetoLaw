namespace LexFlow.Application.Abstractions.Data;

/// <summary>Opta pela transação local compartilhada; o primeiro módulo é o do comando (ADR 0035).</summary>
public interface ILocalTransactionCommand
{
    IReadOnlyList<string> TransactionModules { get; }
}

/// <summary>Coordena unidades de trabalho do mesmo banco e escritório, sem expor DbContexts aos módulos.</summary>
public interface ILocalTransactionCoordinator
{
    Task<IUnitOfWorkTransaction> BeginAsync(IReadOnlyList<string> modules, CancellationToken cancellationToken);
}

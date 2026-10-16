namespace LexFlow.Application.Abstractions.Data;

/// <summary>
/// Transação do módulo. Cada módulo com banco registra a sua (keyed pelo nome do módulo) e o
/// decorator de transação a resolve a partir do namespace do comando (<c>LexFlow.{Modulo}.Application</c>).
/// </summary>
public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }

    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

/// <summary>Transação aberta. Descartar sem <see cref="CommitAsync"/> desfaz as alterações.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}

/// <summary>Nome do módulo a partir do namespace: <c>LexFlow.Crm.Application.X</c> → <c>Crm</c>.</summary>
public static class ModuleName
{
    public static string? Of(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var parts = type.Namespace?.Split('.') ?? [];
        return parts.Length >= 3
            && string.Equals(parts[0], "LexFlow", StringComparison.Ordinal)
            && string.Equals(parts[2], "Application", StringComparison.Ordinal)
            ? parts[1]
            : null;
    }
}

using LexFlow.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LexFlow.Infrastructure.Common.Persistence;

internal sealed class LocalTransaction(DbContext[] contexts, NpgsqlDataSource dataSource, Action release) : IUnitOfWorkTransaction
{
    private readonly Guid? _organization = ((IModuleDbContext)contexts[0]).CurrentOrganizationId;
    private readonly List<DbContext> _enlisted = [];
    private IDbContextTransaction? _owner;
    private bool _completed;
    private bool _committed;
    private bool _disposed;

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        _owner = await contexts[0].Database.BeginTransactionAsync(cancellationToken);
        var connection = contexts[0].Database.GetDbConnection();
        foreach (var context in contexts.Skip(1))
        {
            context.Database.SetDbConnection(connection, contextOwnsConnection: false);
            _enlisted.Add(context);
            await context.Database.UseTransactionAsync(_owner.GetDbTransaction(), cancellationToken);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        EnsureActive();
        if (contexts.Any(context => ((IModuleDbContext)context).CurrentOrganizationId != _organization))
        {
            throw new InvalidOperationException("O escritório não pode mudar durante a transação local.");
        }

        await _owner!.CommitAsync(cancellationToken);
        _completed = true;
        _committed = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        EnsureActive();
        await _owner!.RollbackAsync(cancellationToken);
        _completed = true;
        ClearTrackedChanges();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_owner is not null && !_completed)
            {
                await _owner.RollbackAsync(CancellationToken.None);
            }
        }
        finally
        {
            await DetachAsync();
        }
    }

    private async Task DetachAsync()
    {
        try
        {
            foreach (var context in _enlisted)
            {
                if (context.Database.CurrentTransaction is { } transaction)
                {
                    await transaction.DisposeAsync();
                }

                context.Database.SetDbConnection(dataSource.CreateConnection(), contextOwnsConnection: true);
            }
        }
        finally
        {
            try
            {
                if (_owner is not null)
                {
                    await _owner.DisposeAsync();
                }
            }
            finally
            {
                if (!_committed)
                {
                    ClearTrackedChanges();
                }

                release();
            }
        }
    }

    private void ClearTrackedChanges()
    {
        foreach (var context in contexts)
        {
            context.ChangeTracker.Clear();
        }
    }

    private void EnsureActive()
    {
        if (_disposed || _completed || _owner is null)
        {
            throw new InvalidOperationException("A transação local não está ativa.");
        }
    }
}

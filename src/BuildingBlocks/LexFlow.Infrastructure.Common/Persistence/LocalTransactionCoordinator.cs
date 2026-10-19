using System.Data;
using System.Transactions;
using LexFlow.Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LexFlow.Infrastructure.Common.Persistence;

/// <summary>Exceção optante à unidade de trabalho por módulo; ver ADR 0035.</summary>
internal sealed class LocalTransactionCoordinator(IServiceProvider services, NpgsqlDataSource dataSource) : ILocalTransactionCoordinator
{
    private bool _active;

    public async Task<IUnitOfWorkTransaction> BeginAsync(IReadOnlyList<string> modules, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(modules);
        cancellationToken.ThrowIfCancellationRequested();
        if (_active || Transaction.Current is not null)
        {
            throw new InvalidOperationException("A transação local não pode ser aninhada em outra transação.");
        }

        var contexts = Resolve(modules);
        var transaction = new LocalTransaction(contexts, dataSource, () => _active = false);
        _active = true;
        try
        {
            await transaction.BeginAsync(cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private DbContext[] Resolve(IReadOnlyList<string> modules)
    {
        if (modules.Count < 2 || modules.Any(string.IsNullOrWhiteSpace)
            || modules.Distinct(StringComparer.Ordinal).Count() != modules.Count)
        {
            throw new InvalidOperationException("A transação local exige pelo menos dois módulos distintos.");
        }

        var contexts = modules.Select(module => services.GetRequiredKeyedService<IUnitOfWork>(module) as DbContext
            ?? throw new InvalidOperationException("A unidade de trabalho não é um contexto de módulo.")).ToArray();
        var organization = (contexts[0] as IModuleDbContext)?.CurrentOrganizationId;
        if (organization is null || organization == Guid.Empty || contexts.Distinct().Count() != contexts.Length)
        {
            throw new InvalidOperationException("A transação local exige contextos distintos e um escritório definido.");
        }

        foreach (var context in contexts)
        {
            Validate(context, organization.Value);
        }

        return contexts;
    }

    private void Validate(DbContext context, Guid organization)
    {
        if (context is not IModuleDbContext module || module.CurrentOrganizationId != organization
            || module.HasActiveTransaction || context.Database.GetDbConnection().State != ConnectionState.Closed
            || context.ChangeTracker.HasChanges() || !UsesSameDatabase(context))
        {
            throw new InvalidOperationException("Os módulos precisam do mesmo banco e escritório, sem conexão, transação ou alterações pendentes.");
        }
    }

    private bool UsesSameDatabase(DbContext context)
    {
        if (context.Database.GetDbConnection() is not NpgsqlConnection connection)
        {
            return false;
        }

        // O Npgsql pode ocultar a senha no ConnectionString depois da primeira abertura.
        var actual = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        var expected = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        actual.Remove("Password");
        expected.Remove("Password");
        return actual.EquivalentTo(expected);
    }
}

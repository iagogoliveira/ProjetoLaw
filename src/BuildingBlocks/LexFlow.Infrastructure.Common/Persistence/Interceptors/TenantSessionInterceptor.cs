using System.Data.Common;
using LexFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LexFlow.Infrastructure.Common.Persistence.Interceptors;

/// <summary>
/// Leva o tenant ao PostgreSQL (seção 6.2, item 5): grava <c>app.organization_id</c> na sessão da conexão quando ela
/// abre e de novo antes de um comando se o tenant mudou desde então (onboarding, com o <c>TenantOverride</c>). As
/// políticas de Row-Level Security leem esse valor; vazio = nenhuma linha de escritório. O valor é de sessão, não de
/// transação, porque o EF lê fora de transação explícita; ao voltar ao pool, o Npgsql descarta o estado da sessão, e
/// toda abertura grava o valor de novo.
/// </summary>
internal sealed class TenantSessionInterceptor(ITenantContext tenant) : DbConnectionInterceptor, IDbCommandInterceptor
{
    public const string SettingName = "app.organization_id";

    private DbConnection? _connection;
    private string? _applied;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Apply(connection, transaction: null);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) =>
        ApplyAsync(connection, transaction: null, cancellationToken);

    public InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        EnsureCurrent(command);
        return result;
    }

    public async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await EnsureCurrentAsync(command, cancellationToken);
        return result;
    }

    public InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        EnsureCurrent(command);
        return result;
    }

    public async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await EnsureCurrentAsync(command, cancellationToken);
        return result;
    }

    public InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        EnsureCurrent(command);
        return result;
    }

    public async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        await EnsureCurrentAsync(command, cancellationToken);
        return result;
    }

    private string Desired => tenant.OrganizationId?.ToString("D") ?? string.Empty;

    private void EnsureCurrent(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Connection is { } connection && (!ReferenceEquals(connection, _connection) || !string.Equals(_applied, Desired, StringComparison.Ordinal)))
        {
            Apply(connection, command.Transaction);
        }
    }

    private async Task EnsureCurrentAsync(DbCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Connection is { } connection && (!ReferenceEquals(connection, _connection) || !string.Equals(_applied, Desired, StringComparison.Ordinal)))
        {
            await ApplyAsync(connection, command.Transaction, cancellationToken);
        }
    }

    private void Apply(DbConnection connection, DbTransaction? transaction)
    {
        using var command = CreateCommand(connection, transaction, out var value);
        command.ExecuteNonQuery();
        Remember(connection, value);
    }

    private async Task ApplyAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, transaction, out var value);
        await command.ExecuteNonQueryAsync(cancellationToken);
        Remember(connection, value);
    }

    private DbCommand CreateCommand(DbConnection connection, DbTransaction? transaction, out string value)
    {
        value = Desired;
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT set_config('{SettingName}', @organization, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "organization";
        parameter.Value = value;
        command.Parameters.Add(parameter);
        return command;
    }

    private void Remember(DbConnection connection, string value)
    {
        _connection = connection;
        _applied = value;
    }
}

using Dapper;
using LexFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LexFlow.Infrastructure.Common.Persistence;

/// <summary>Leituras parametrizadas no contexto do próprio módulo, preservando a sessão RLS e a transação.</summary>
public static class TenantSql
{
    public static async Task<IReadOnlyList<T>> QueryAsync<T>(DbContext db, ITenantContext tenant, string sql,
        object parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(tenant);
        if (tenant.OrganizationId is null) { throw new InvalidOperationException("A consulta exige escritório ativo."); }
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = db.Database.GetDbConnection();
            var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            // Dapper não executa interceptors de comando do EF; reaplica o tenant também em conexão já aberta.
            await connection.ExecuteAsync(new CommandDefinition("SELECT set_config('app.organization_id', @Organization, false)",
                new { Organization = tenant.OrganizationId.Value.ToString("D") }, transaction, cancellationToken: cancellationToken));
            var result = await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, transaction,
                cancellationToken: cancellationToken));
            return result.AsList();
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}

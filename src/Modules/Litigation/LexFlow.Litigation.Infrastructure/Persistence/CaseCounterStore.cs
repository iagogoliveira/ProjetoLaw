using LexFlow.Application.Abstractions;
using LexFlow.Litigation.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Infrastructure.Persistence;

/// <summary>
/// Próximo número da sequência por área e ano, num único comando atômico do PostgreSQL (parametrizado). Roda na
/// transação aberta pelo caso de uso: se o cadastro falhar, o número volta.
/// </summary>
internal sealed class CaseCounterStore(LitigationDbContext db, ITenantContext tenant) : ICaseCounter
{
    public async Task<long> NextAsync(Guid? practiceAreaId, int year, CancellationToken cancellationToken)
    {
        var organization = tenant.RequiredOrganizationId;
        var area = practiceAreaId ?? Guid.Empty;
        // Comando não composto (INSERT … RETURNING): lido inteiro, sem LIMIT do EF.
        var values = await db.Database.SqlQuery<long>(
            $"""
            INSERT INTO litigation.case_counters (organization_id, practice_area_key, year, last_value)
            VALUES ({organization}, {area}, {year}, 1)
            ON CONFLICT (organization_id, practice_area_key, year)
            DO UPDATE SET last_value = litigation.case_counters.last_value + 1
            RETURNING last_value AS "Value"
            """).ToListAsync(cancellationToken);
        return values.Single();
    }
}

using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Domain.Claims;
using LexFlow.Litigation.Domain.Hearings;
using LexFlow.Litigation.Domain.Numbering;
using LexFlow.Litigation.Domain.Risk;
using LexFlow.Litigation.Domain.Settlements;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Abstractions;

/// <summary>Acesso aos dados do módulo pelos handlers (implementado pelo <c>LitigationDbContext</c>).</summary>
public interface ILitigationDbContext
{
    DbSet<LegalCase> Cases { get; }

    DbSet<CaseRelation> Relations { get; }

    DbSet<CaseNumbering> Numberings { get; }

    DbSet<Claim> Claims { get; }

    DbSet<RiskAssessment> RiskAssessments { get; }

    DbSet<Settlement> Settlements { get; }

    DbSet<Hearing> Hearings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Contador da numeração interna por organização, área e ano (decisão de 2026-10-18). Incrementa no banco, na
/// transação do caso de uso (<c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c>): dois cadastros simultâneos nunca
/// recebem o mesmo número.
/// </summary>
public interface ICaseCounter
{
    Task<long> NextAsync(Guid? practiceAreaId, int year, CancellationToken cancellationToken);
}

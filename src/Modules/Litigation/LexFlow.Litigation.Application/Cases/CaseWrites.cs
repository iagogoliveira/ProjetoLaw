using LexFlow.Application.Abstractions;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Cases;

/// <summary>
/// Conferências comuns às alterações dos dados ligados a um processo (pedidos, risco, acordos e audiências): o
/// processo existe e está aberto. Também entrega quem altera e quando.
/// </summary>
internal sealed class CaseWrites(ILitigationDbContext db, ICurrentUser user, IClock clock)
{
    public Guid UserId => user.UserId ?? throw new InvalidOperationException("Processo alterado sem usuário autenticado.");

    public DateTimeOffset Now => clock.UtcNow;

    public DateOnly Today => clock.Today;

    public async Task<Result> CheckOpenAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var status = await db.Cases.Where(c => c.Id == caseId).Select(c => (CaseStatus?)c.Status).SingleOrDefaultAsync(cancellationToken);
        return status switch
        {
            null => LitigationErrors.CaseNotFound,
            CaseStatus.Closed => LitigationErrors.CaseClosed,
            _ => Result.Success,
        };
    }

    public Task<bool> ExistsAsync(Guid caseId, CancellationToken cancellationToken) => db.Cases.AnyAsync(c => c.Id == caseId, cancellationToken);

    /// <summary>Registra a mudança de situação (para a linha do tempo), se houve; grava junto com o processo.</summary>
    public void RecordStatus(Guid caseId, CaseStatus from, CaseStatus to, string? note)
    {
        if (CaseStatusChange.Between(caseId, from, to, note, UserId, Now) is { } change)
        {
            db.StatusChanges.Add(change);
        }
    }
}

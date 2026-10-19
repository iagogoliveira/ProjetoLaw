using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain.Hearings;
using LexFlow.Litigation.Domain.Settlements;
using Microsoft.EntityFrameworkCore;
using static LexFlow.Litigation.Application.Timeline.TimelineValues;

namespace LexFlow.Litigation.Application.Timeline;

/// <summary>
/// Os acontecimentos do próprio Litigation: cadastro e mudanças de situação do processo, pedidos (criação e alteração
/// de valor), avaliações de risco, acordos (abertura, rodadas, envio, aprovação, homologação, baixas, fim sem acordo) e
/// audiências (agendamento, redesignações, realização, cancelamento). O volume por processo é pequeno: monta tudo e
/// pagina em memória.
/// </summary>
internal sealed class LitigationTimeline(ILitigationDbContext db) : ITimelineContributor
{
    public async Task<IReadOnlyList<TimelineEntry>> GetAsync(Guid caseId, TimelinePosition? before, int limit, CancellationToken cancellationToken)
    {
        var entries = new List<TimelineEntry>();
        await AddCaseAsync(caseId, entries, cancellationToken);
        await AddClaimsAndRiskAsync(caseId, entries, cancellationToken);
        await AddSettlementsAsync(caseId, entries, cancellationToken);
        await AddHearingsAsync(caseId, entries, cancellationToken);
        return [.. entries
            .Where(e => before is null || before.Precedes(e))
            .OrderByDescending(e => e.At).ThenByDescending(e => e.Id, StringComparer.Ordinal)
            .Take(limit)];
    }

    private static TimelineEntry Entry(string id, DateTimeOffset at, string kind, Guid? reference, params (string Key, string? Value)[] data) =>
        new(id, at, "litigation." + kind, data.ToDictionary(d => d.Key, d => d.Value, StringComparer.Ordinal), reference);

    private async Task AddCaseAsync(Guid caseId, List<TimelineEntry> entries, CancellationToken cancellationToken)
    {
        var legalCase = await db.Cases.AsNoTracking().Where(c => c.Id == caseId)
            .Select(c => new { c.CreatedAt, c.CreatedBy, c.InternalFolder }).SingleAsync(cancellationToken);
        entries.Add(Entry($"case:{caseId:N}:created", legalCase.CreatedAt, "case_created", caseId,
            ("folder", legalCase.InternalFolder), ("by", Of(legalCase.CreatedBy))));

        foreach (var change in await db.StatusChanges.AsNoTracking().Where(s => s.LegalCaseId == caseId).ToListAsync(cancellationToken))
        {
            entries.Add(Entry($"status:{change.Id:N}", change.ChangedAt, "status_changed", caseId,
                ("from", change.From.ToString()), ("to", change.To.ToString()), ("note", change.Note), ("by", Of(change.ChangedBy))));
        }
    }

    private async Task AddClaimsAndRiskAsync(Guid caseId, List<TimelineEntry> entries, CancellationToken cancellationToken)
    {
        var claims = await db.Claims.AsNoTracking().Include(c => c.Changes).Where(c => c.LegalCaseId == caseId).ToListAsync(cancellationToken);
        foreach (var claim in claims)
        {
            entries.Add(Entry($"claim:{claim.Id:N}:created", claim.CreatedAt, "claim_created", claim.Id,
                ("claimTypeId", Of(claim.ClaimTypeId)), ("by", Of(claim.CreatedBy))));

            // Só as alterações de verdade: os valores iniciais já estão no cadastro do pedido.
            foreach (var change in claim.Changes.Where(c => c.OldAmount is not null))
            {
                entries.Add(Entry($"claim-value:{change.Id:N}", change.ChangedAt, "claim_value_changed", claim.Id,
                    ("instance", change.Instance.ToString()), ("kind", change.Kind.ToString()),
                    ("old", Of(change.OldAmount)), ("new", Of(change.NewAmount)), ("by", Of(change.ChangedBy))));
            }
        }

        foreach (var risk in await db.RiskAssessments.AsNoTracking().Where(r => r.LegalCaseId == caseId).ToListAsync(cancellationToken))
        {
            entries.Add(Entry($"risk:{risk.Id:N}", risk.AssessedAt, "risk_assessed", risk.ClaimId ?? caseId,
                ("probability", risk.Probability.ToString()), ("estimatedLoss", Of(risk.EstimatedLoss)),
                ("claimId", risk.ClaimId is { } claimId ? Of(claimId) : null), ("by", Of(risk.AssessedBy))));
        }
    }

    private async Task AddSettlementsAsync(Guid caseId, List<TimelineEntry> entries, CancellationToken cancellationToken)
    {
        var settlements = await db.Settlements.AsNoTracking().Include(s => s.Offers).Include(s => s.Installments)
            .Where(s => s.LegalCaseId == caseId).ToListAsync(cancellationToken);
        foreach (var s in settlements)
        {
            var key = $"settlement:{s.Id:N}";
            entries.Add(Entry($"{key}:started", s.CreatedAt, "settlement_started", s.Id, ("direction", s.Direction.ToString()), ("by", Of(s.CreatedBy))));
            entries.AddRange(s.Offers.Select(o => Entry($"{key}:offer:{o.Round}", At(o.OfferedOn), "settlement_offer", s.Id,
                ("round", o.Round.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("party", o.Party.ToString()), ("amount", Of(o.Amount)))));
            if (s.SubmittedAt is { } submitted)
            {
                entries.Add(Entry($"{key}:submitted", submitted, "settlement_submitted", s.Id, ("amount", Of(s.AgreedAmount)), ("by", s.SubmittedBy is { } by ? Of(by) : null)));
            }

            if (s.ApprovedAt is { } approved)
            {
                entries.Add(Entry($"{key}:approved", approved, "settlement_approved", s.Id, ("by", s.ApprovedBy is { } by ? Of(by) : null)));
            }

            if (s.HomologatedOn is { } homologated)
            {
                entries.Add(Entry($"{key}:homologated", At(homologated), "settlement_homologated", s.Id, ("amount", Of(s.AgreedAmount))));
            }

            entries.AddRange(s.Installments.Where(i => i.PaidOn is not null).Select(i => Entry($"{key}:paid:{i.Number}", At(i.PaidOn!.Value), "installment_paid", s.Id,
                ("number", i.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("amount", Of(i.PaidAmount)))));
            if (s.Status is SettlementStatus.Rejected or SettlementStatus.Cancelled)
            {
                entries.Add(Entry($"{key}:ended", s.UpdatedAt ?? s.CreatedAt, "settlement_ended", s.Id, ("status", s.Status.ToString()), ("reason", s.Reason)));
            }
        }
    }

    private async Task AddHearingsAsync(Guid caseId, List<TimelineEntry> entries, CancellationToken cancellationToken)
    {
        var hearings = await db.Hearings.AsNoTracking().Include(h => h.Reschedules).Where(h => h.LegalCaseId == caseId).ToListAsync(cancellationToken);
        foreach (var h in hearings)
        {
            var key = $"hearing:{h.Id:N}";
            var first = h.Reschedules.OrderBy(r => r.ChangedAt).FirstOrDefault()?.PreviousDate ?? h.ScheduledAt;
            entries.Add(Entry($"{key}:scheduled", h.CreatedAt, "hearing_scheduled", h.Id,
                ("type", h.Type.ToString()), ("format", h.Format.ToString()), ("scheduledAt", Of(first)), ("by", Of(h.CreatedBy))));
            entries.AddRange(h.Reschedules.Select(r => Entry($"{key}:rescheduled:{r.Id:N}", r.ChangedAt, "hearing_rescheduled", h.Id,
                ("from", Of(r.PreviousDate)), ("to", Of(r.NewDate)), ("reason", r.Reason), ("by", Of(r.ChangedBy)))));
            if (h.Status != HearingStatus.Scheduled)
            {
                var ending = h.Status == HearingStatus.Held ? "held" : "cancelled";
                entries.Add(Entry($"{key}:{ending}", h.UpdatedAt ?? h.CreatedAt, "hearing_" + ending, h.Id,
                    ("outcome", h.Outcome), ("by", h.UpdatedBy is { } by ? Of(by) : null)));
            }
        }
    }
}

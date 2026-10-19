using System.Security.Cryptography;
using System.Text;
using LexFlow.Application.Abstractions;
using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Closing;

internal sealed record ClosingSnapshot(int PublicationCount, int PendingCount, int MissingCaptureCount, string CaptureFingerprint, DailyClose? Latest)
{
    public bool IsClosed => PendingCount == 0 && MissingCaptureCount == 0 && Latest is not null
        && Latest.PublicationCount == PublicationCount && string.Equals(Latest.CaptureFingerprint, CaptureFingerprint, StringComparison.Ordinal);
}

internal sealed class DailyCloseState(IPublicationsDbContext db, IClock clock)
{
    public async Task<ClosingSnapshot> ReadAsync(DateOnly day, CancellationToken cancellationToken)
    {
        var end = LegalDay.Start(day.AddDays(1), clock.LegalTimeZone);
        var count = await db.Publications.CountAsync(p => p.ReceivedAt < end, cancellationToken);
        var pending = await db.Publications.CountAsync(p => p.ReceivedAt < end && p.Status == PublicationStatus.Pending, cancellationToken);
        var (missing, fingerprint) = await CapturesAsync(day, end, cancellationToken);
        var latest = await db.DailyCloses.AsNoTracking().Where(c => c.Day == day).OrderByDescending(c => c.Revision).FirstOrDefaultAsync(cancellationToken);
        return new ClosingSnapshot(count, pending, missing, fingerprint, latest);
    }

    private async Task<(int Missing, string Fingerprint)> CapturesAsync(DateOnly day, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var window = LegalDay.At(day, new TimeOnly(5, 0), clock.LegalTimeZone);
        var periods = await db.SourcePeriods.AsNoTracking().Where(p => p.ActivatedAt < end && (p.DeactivatedAt == null || p.DeactivatedAt > window))
            .ToListAsync(cancellationToken);
        // Como no monitor do 6B, cada fonte/dia exige sucesso na última ativação aplicável.
        var expected = periods.GroupBy(p => p.SourceId).Select(g => g.OrderBy(p => p.ActivatedAt)
            .ThenBy(p => p.DeactivatedAt ?? DateTimeOffset.MaxValue).ThenBy(p => p.Id).Last()).ToList();
        var runs = await db.Runs.AsNoTracking().Where(r => r.Day == day && r.Status == CaptureRunStatus.Succeeded)
            .Select(r => new { r.SourceId, r.SourceActivatedAt, r.StartedAt }).ToListAsync(cancellationToken);
        var legacyStarts = await db.Sources.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.CreatedAt, cancellationToken);
        var missing = expected.Count(p => !runs.Any(r => r.SourceId == p.SourceId
            && (r.SourceActivatedAt == p.ActivatedAt || r.SourceActivatedAt == legacyStarts[p.SourceId]
                && r.StartedAt >= p.ActivatedAt && (p.DeactivatedAt == null || r.StartedAt < p.DeactivatedAt))));
        var earlierAlerts = await db.Alerts.CountAsync(a => a.Day < day && a.ResolvedAt == null, cancellationToken);
        var material = string.Join('\n', expected.Select(p => p.Id.ToString("N")).Order(StringComparer.Ordinal));
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return (missing + earlierAlerts, fingerprint);
    }
}

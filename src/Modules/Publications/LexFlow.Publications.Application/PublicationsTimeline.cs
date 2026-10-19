using LexFlow.Litigation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

internal sealed class PublicationsTimeline(IPublicationsDbContext db) : ITimelineContributor
{
    private static Guid PositionId(TimelinePosition before, string prefix) =>
        before.Id.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(before.Id.AsSpan(prefix.Length), "N", out var id)
            ? id : string.CompareOrdinal(prefix, before.Id) < 0 ? Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff") : Guid.Empty;

    public async Task<IReadOnlyList<TimelineEntry>> GetAsync(Guid caseId, TimelinePosition? before, int limit, CancellationToken cancellationToken)
    {
        // Aplica a data no banco; o desempate final usa o id textual estável do contrato.
        var movements = db.Movements.AsNoTracking().Where(m => m.LegalCaseId == caseId);
        var publications = db.Publications.AsNoTracking().Where(p => p.LegalCaseId == caseId);
        if (before is not null)
        {
            var movementId = PositionId(before, "movement:");
            var publicationId = PositionId(before, "publication:");
            movements = movements.Where(m => m.OccurredAt < before.At || (m.OccurredAt == before.At && m.Id.CompareTo(movementId) < 0));
            publications = publications.Where(p => p.ReceivedAt < before.At || (p.ReceivedAt == before.At && p.Id.CompareTo(publicationId) < 0));
        }

        var movementEntries = (await movements.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.Id).Take(limit + 1).ToListAsync(cancellationToken))
            .Select(m => new TimelineEntry($"movement:{m.Id:N}", m.OccurredAt, "publications.movement", new Dictionary<string, string?>(StringComparer.Ordinal)
            { ["description"] = m.Description, ["origin"] = m.Origin.ToString() }, m.Id));
        var publicationEntries = (await publications.OrderByDescending(p => p.ReceivedAt).ThenByDescending(p => p.Id).Take(limit + 1).ToListAsync(cancellationToken))
            .Select(p => new TimelineEntry($"publication:{p.Id:N}", p.ReceivedAt, "publications.received", new Dictionary<string, string?>(StringComparer.Ordinal)
            { ["journal"] = p.Journal }, p.Id));
        return movementEntries.Concat(publicationEntries).Where(e => before is null || before.Precedes(e))
            .OrderByDescending(e => e.At).ThenByDescending(e => e.Id, StringComparer.Ordinal).Take(limit).ToList();
    }
}

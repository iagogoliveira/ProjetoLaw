using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.Calendar;
using LexFlow.Platform.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Calendar;

internal sealed class HolidayDirectory(IPlatformDbContext db) : IHolidayDirectory
{
    public async Task<IReadOnlyList<PlatformNonWorkingDay>> ForAsync(DateOnly fromDate, DateOnly toDate, string? stateCode, bool federalJustice,
        CancellationToken cancellationToken)
    {
        var state = stateCode?.Trim().ToUpperInvariant();
        var rules = await db.Holidays.AsNoTracking()
            .Where(h => h.Scope == HolidayScope.National
                || (h.Scope == HolidayScope.State && h.StateCode == state)
                || (h.Scope == HolidayScope.FederalJustice && federalJustice))
            .ToListAsync(cancellationToken);

        // Um período que começa no fim do ano anterior (ex.: recesso de 20/12 a 20/01) também cobre o início do intervalo.
        var days = new List<PlatformNonWorkingDay>();
        for (var year = fromDate.Year - 1; year <= toDate.Year; year++)
        {
            foreach (var rule in rules.Where(r => r.AppliesTo(year)))
            {
                days.AddRange(rule.DatesIn(year).Where(d => d >= fromDate && d <= toDate)
                    .Select(d => new PlatformNonWorkingDay(d, rule.Name, rule.Kind == HolidayKind.Suspension, rule.LegalBasis)));
            }
        }

        return [.. days.OrderBy(d => d.Date).ThenBy(d => d.Name, StringComparer.Ordinal)];
    }
}

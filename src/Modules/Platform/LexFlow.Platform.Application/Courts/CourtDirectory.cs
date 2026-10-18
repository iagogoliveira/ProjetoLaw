using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.Courts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Courts;

internal sealed class CourtDirectory(IPlatformDbContext db) : ICourtDirectory
{
    public Task<CourtInfo?> FindAsync(int segment, string code, CancellationToken cancellationToken) =>
        db.Courts.AsNoTracking()
            .Where(c => c.Segment == segment && c.Code == code)
            .Select(c => new CourtInfo(c.Id, c.Segment, c.Code, c.Acronym, c.Name, c.State))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<CourtInfo?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Courts.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourtInfo(c.Id, c.Segment, c.Code, c.Acronym, c.Name, c.State))
            .FirstOrDefaultAsync(cancellationToken);
}

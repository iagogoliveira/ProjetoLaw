using LexFlow.Publications.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Capture;

internal sealed class CaptureAlertDirectory(IPublicationsDbContext db) : ICaptureAlertDirectory
{
    public async Task<CaptureAlertInfo?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await (from alert in db.Alerts.AsNoTracking()
               join source in db.Sources.AsNoTracking() on alert.SourceId equals source.Id
               where alert.Id == id
               select new CaptureAlertInfo(alert.Id, source.Id, alert.Day, source.Label, source.IsActive, alert.ResolvedAt != null))
            .SingleOrDefaultAsync(cancellationToken);
}

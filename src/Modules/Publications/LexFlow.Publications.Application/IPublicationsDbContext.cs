using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

public interface IPublicationsDbContext
{
    DbSet<CaptureSource> Sources { get; }
    DbSet<CaptureRun> Runs { get; }
    DbSet<Publication> Publications { get; }
    DbSet<PublicationTriage> Triages { get; }
    DbSet<CaseMovement> Movements { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

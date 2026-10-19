using LexFlow.Application.Abstractions.Data;
using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

public interface IPublicationsDbContext : IUnitOfWork
{
    DbSet<CaptureSource> Sources { get; }
    DbSet<CaptureAlert> Alerts { get; }
    DbSet<CaptureRun> Runs { get; }
    DbSet<Publication> Publications { get; }
    DbSet<PublicationTriage> Triages { get; }
    DbSet<CaseMovement> Movements { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

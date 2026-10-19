using LexFlow.Application.Abstractions.Data;
using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application;

public interface IPublicationsDbContext : IUnitOfWork
{
    DbSet<DailyClose> DailyCloses { get; }
    DbSet<CaptureSourcePeriod> SourcePeriods { get; }
    DbSet<CaptureSource> Sources { get; }
    DbSet<CaptureAlert> Alerts { get; }
    DbSet<CaptureRun> Runs { get; }
    DbSet<Publication> Publications { get; }
    DbSet<PublicationTriage> Triages { get; }
    DbSet<CaseMovement> Movements { get; }
    DbSet<MovementIntegration> MovementIntegrations { get; }
    DbSet<MovementSync> MovementSyncs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

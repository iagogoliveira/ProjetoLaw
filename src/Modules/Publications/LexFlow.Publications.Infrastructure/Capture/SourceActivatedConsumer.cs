using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Infrastructure.Capture;

public sealed class SourceActivatedConsumer(PublicationsDbContext db, IClock clock, IJobScheduler jobs)
    : IntegrationEventConsumer<SourceActivatedIntegrationEvent, PublicationsDbContext>(db, clock)
{
    private readonly IClock _clock = clock;
    protected override async Task ConsumeAsync(SourceActivatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var source = await Db.Sources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == integrationEvent.SourceId, cancellationToken);
        if (source is null || !source.IsActive) { return; }
        var day = _clock.Today;
        var activationDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(CaptureSchedule.ActiveSince(source), _clock.LegalTimeZone).DateTime);
        if (activationDay == day.AddDays(-1) && _clock.UtcNow < CaptureSchedule.Deadline(source, activationDay, _clock.LegalTimeZone))
        {
            day = activationDay;
        }
        if (CaptureSchedule.CanCapture(source, day, _clock.UtcNow, _clock.LegalTimeZone, force: false))
        {
            jobs.Enqueue<CaptureSourceJob>(job => job.RunAsync(integrationEvent.SourceId, day, false));
        }
        var delay = CaptureSchedule.Deadline(source, day, _clock.LegalTimeZone) - _clock.UtcNow;
        jobs.Schedule<CaptureMonitor>(job => job.RunAsync(integrationEvent.SourceId, day), delay > TimeSpan.Zero ? delay : TimeSpan.Zero);
    }
}

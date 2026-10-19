using System.Diagnostics.Metrics;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Capture;

public static class CaptureMetrics
{
    public const string MeterName = "LexFlow.Publications";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Missing = Meter.CreateCounter<long>("lexflow_capture_missing_total");
    public static void MissingCapture() => Missing.Add(1);
}

public sealed class CaptureMonitor(IPublicationsDbContext db, ICaptureLocks locks, IClock clock, IIntegrationEventPublisher events)
{
    public async Task RunAsync(Guid sourceId, DateOnly day)
    {
        var ct = CancellationToken.None;
        await using var transaction = await db.BeginTransactionAsync(ct);
        await locks.ControlAsync(ct);
        if (!await locks.TrySourceAsync(sourceId, day, ct)) { return; }
        var source = await db.Sources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sourceId, ct);
        if (source is null || !source.IsActive || day > clock.Today || day < clock.Today.AddDays(-1)
            || DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(CaptureSchedule.ActiveSince(source), clock.LegalTimeZone).DateTime) > day
            || clock.UtcNow < CaptureSchedule.Deadline(source, day, clock.LegalTimeZone)) { return; }
        var activation = CaptureSchedule.ActiveSince(source);
        if (await db.Runs.AnyAsync(r => r.SourceId == sourceId && r.Day == day && r.SourceActivatedAt == activation && r.Status == CaptureRunStatus.Succeeded, ct))
        {
            return;
        }
        var errorCode = await db.Runs.Where(r => r.SourceId == sourceId && r.Day == day && r.SourceActivatedAt == activation)
            .OrderByDescending(r => r.StartedAt).Select(r => r.ErrorCode).FirstOrDefaultAsync(ct) ?? "publications.capture_missing";
        var alert = await db.Alerts.SingleOrDefaultAsync(a => a.SourceId == sourceId && a.Day == day, ct);
        var changed = alert is null;
        if (alert is null)
        {
            alert = CaptureAlert.Raise(sourceId, day, clock.UtcNow, errorCode);
            db.Alerts.Add(alert);
        }
        else { changed = alert.Reopen(clock.UtcNow, errorCode); }
        if (changed) { events.Publish(new CaptureMissingIntegrationEvent(alert.Id, sourceId, day)); }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (changed) { CaptureMetrics.MissingCapture(); }
    }
}

using LexFlow.Publications.Domain;

namespace LexFlow.Publications.Application.Capture;

public interface ICaptureLocks
{
    Task<bool> TrySourceAsync(Guid sourceId, DateOnly day, CancellationToken cancellationToken);
    Task PublicationAsync(string hash, CancellationToken cancellationToken);
}
public static class CaptureSchedule
{
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(10);
    public static DateTimeOffset ActiveSince(CaptureSource source) => source.ActivatedAt ?? source.CreatedAt;
    public static DateTimeOffset Deadline(CaptureSource source, DateOnly day, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(zone);
        var local = day.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Unspecified);
        var cutoff = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
        return ActiveSince(source) > cutoff ? ActiveSince(source).AddHours(1) : cutoff;
    }
    public static bool CanCapture(CaptureSource source, DateOnly day, DateTimeOffset now, TimeZoneInfo zone, bool force)
    {
        ArgumentNullException.ThrowIfNull(source);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var localDay = DateOnly.FromDateTime(localNow.DateTime);
        var activatedDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(ActiveSince(source), zone).DateTime);
        if (!source.IsActive || ActiveSince(source) > now || day > localDay || day < activatedDay) { return false; }
        if (force) { return true; }
        var overnight = activatedDay == day && localDay == day.AddDays(1);
        return (localDay == day && localNow.Hour >= 5 || overnight) && now < Deadline(source, day, zone);
    }
}

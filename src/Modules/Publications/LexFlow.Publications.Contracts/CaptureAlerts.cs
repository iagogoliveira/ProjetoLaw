namespace LexFlow.Publications.Contracts;

public sealed record CaptureAlertInfo(Guid Id, Guid SourceId, DateOnly Day, string SourceLabel, bool Active, bool Resolved);
public interface ICaptureAlertDirectory
{
    Task<CaptureAlertInfo?> FindAsync(Guid id, CancellationToken cancellationToken);
}

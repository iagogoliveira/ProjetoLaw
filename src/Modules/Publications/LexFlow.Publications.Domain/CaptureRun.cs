using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Domain;

public enum CaptureRunStatus { Running, Succeeded, Failed }

public sealed class CaptureRun : Entity<Guid>, ITenantOwned
{
    private CaptureRun() { }
    public Guid OrganizationId { get; private set; }
    public Guid SourceId { get; private set; }
    public Guid? SourcePeriodId { get; private set; }
    public DateOnly Day { get; private set; }
    public DateTimeOffset? SourceActivatedAt { get; private set; }
    public CaptureRunStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public int Count { get; private set; }
    public string? ErrorCode { get; private set; }

    public static Result<CaptureRun> Start(Guid sourceId, DateOnly day, DateTimeOffset now, DateTimeOffset? sourceActivatedAt = null, Guid? sourcePeriodId = null)
    {
        if (sourceId == Guid.Empty || day == default) { return PublicationErrors.InvalidRun; }
        return new CaptureRun
        {
            Id = Guid.CreateVersion7(),
            SourceId = sourceId,
            SourcePeriodId = sourcePeriodId,
            SourceActivatedAt = sourceActivatedAt?.ToUniversalTime(),
            Day = day,
            StartedAt = now.ToUniversalTime()
        };
    }

    public Result Finish(int count, string? errorCode, DateTimeOffset now)
    {
        if (Status != CaptureRunStatus.Running || count < 0 || now < StartedAt
            || (errorCode is not null && (string.IsNullOrWhiteSpace(errorCode) || errorCode.Length > 100)))
        {
            return PublicationErrors.InvalidRun;
        }

        Count = count;
        ErrorCode = errorCode;
        Status = errorCode is null ? CaptureRunStatus.Succeeded : CaptureRunStatus.Failed;
        FinishedAt = now.ToUniversalTime();
        return Result.Success;
    }
}

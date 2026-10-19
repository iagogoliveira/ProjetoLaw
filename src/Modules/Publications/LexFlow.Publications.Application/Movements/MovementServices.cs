using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Application.Movements;

public interface IMovementSecrets
{
    string Protect(string value);
    Result<string> Unprotect(string value);
}
public interface IConfiguredMovementProvider
{
    Task<Result<MovementPage>> FetchAsync(MovementIntegration configuration, MovementQuery query, CancellationToken cancellationToken);
}

public static class MovementSchedule
{
    private const long IntervalSeconds = 6 * 60 * 60;
    public static DateTimeOffset Slot(Guid caseId, DateTimeOffset now)
    {
        // O mesmo deslocamento em todas as máquinas; não usar Guid.GetHashCode().
        var bytes = caseId.ToByteArray();
        var offset = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes) % IntervalSeconds;
        var seconds = now.ToUnixTimeSeconds() - offset;
        var window = (long)Math.Floor(seconds / (double)IntervalSeconds);
        return DateTimeOffset.FromUnixTimeSeconds((window * IntervalSeconds) + offset);
    }
    public static bool IsDue(MovementSync? state, Guid revision, Guid caseId, DateTimeOffset now) =>
        state is null || state.ConfigurationRevision != revision ||
        ((state.Cursor is not null || state.CompletedSlot != Slot(caseId, now)) &&
            (state.LastAttemptAt is null || state.LastAttemptAt <= now.AddMinutes(-5)));
}

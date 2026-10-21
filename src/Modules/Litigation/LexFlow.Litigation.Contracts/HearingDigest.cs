namespace LexFlow.Litigation.Contracts;

public sealed record HearingDigestItem(Guid Id, Guid CaseId, DateTimeOffset ScheduledAt);
public interface IHearingDigest
{
    Task<IReadOnlyList<HearingDigestItem>> ReadAsync(Guid userId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken);
}

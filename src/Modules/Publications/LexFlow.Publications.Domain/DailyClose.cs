using LexFlow.SharedKernel.Domain;

namespace LexFlow.Publications.Domain;

/// <summary>Fechamento imutável. Novidades exigem outro registro; não se altera a assinatura anterior.</summary>
public sealed class DailyClose : Entity<Guid>, ITenantOwned
{
    private DailyClose() { }
    public Guid OrganizationId { get; private set; }
    public DateOnly Day { get; private set; }
    public int Revision { get; private set; }
    public Guid ClosedBy { get; private set; }
    public DateTimeOffset ClosedAt { get; private set; }
    public int PublicationCount { get; private set; }
    public string CaptureFingerprint { get; private set; } = string.Empty;

    public static DailyClose Create(DateOnly day, int revision, Guid userId, DateTimeOffset now, int publicationCount, string fingerprint) => new()
    {
        Id = Guid.CreateVersion7(),
        Day = day,
        Revision = revision,
        ClosedBy = userId,
        ClosedAt = now.ToUniversalTime(),
        PublicationCount = publicationCount,
        CaptureFingerprint = fingerprint,
    };
}

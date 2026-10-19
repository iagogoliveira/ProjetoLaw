using LexFlow.SharedKernel.Domain;

namespace LexFlow.Publications.Domain;

/// <summary>Checkpoint por processo e tribunal. Cada página e seus andamentos são gravados juntos.</summary>
public sealed class MovementSync : Entity<Guid>, ITenantOwned, ICaseScoped
{
    private MovementSync() { }
    public Guid OrganizationId { get; private set; }
    public Guid LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public string Tribunal { get; private set; } = string.Empty;
    public Guid ConfigurationRevision { get; private set; }
    public string? Cursor { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? LastSuccessAt { get; private set; }
    public DateTimeOffset? CompletedSlot { get; private set; }
    public string? ErrorCode { get; private set; }
    public int PageCount { get; private set; }
    public static MovementSync Create(Guid caseId) => new() { Id = Guid.CreateVersion7(), LegalCaseId = caseId };

    public void ResetFor(Guid revision)
    {
        if (ConfigurationRevision == revision) { return; }
        ConfigurationRevision = revision;
        Cursor = null;
        PageCount = 0;
        CompletedSlot = null;
        ErrorCode = null;
    }
    public void Attempt(DateTimeOffset now) => LastAttemptAt = now.ToUniversalTime();
    public void Fail(string error) => ErrorCode = error;
    public void Restart()
    {
        Cursor = null;
        PageCount = 0;
        CompletedSlot = null;
    }
    public void AcceptPage(string tribunal, string? nextCursor, DateTimeOffset slot, DateTimeOffset now)
    {
        Tribunal = tribunal;
        Cursor = nextCursor;
        ErrorCode = null;
        PageCount++;
        if (nextCursor is not null) { return; }
        LastSuccessAt = now.ToUniversalTime();
        CompletedSlot = slot.ToUniversalTime();
        PageCount = 0;
    }
}

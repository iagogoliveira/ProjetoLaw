using LexFlow.SharedKernel.Domain;

namespace LexFlow.Scheduling.Domain.CalendarSync;

public sealed class CalendarSyncLink : Entity<Guid>, ITenantOwned, ICaseScoped
{
    private CalendarSyncLink() { }
    public Guid OrganizationId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public Guid? LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public CalendarItemKind Kind { get; private set; }
    public Guid? LocalId { get; private set; }
    public string RemoteId { get; private set; } = string.Empty;
    public string RemoteVersion { get; private set; } = string.Empty;
    [Sensitive]
    public string RemoteSnapshot { get; private set; } = "null";
    public string LocalFingerprint { get; private set; } = string.Empty;
    public CalendarPendingReason? PendingReason { get; private set; }
    [Sensitive]
    public string? PendingSnapshot { get; private set; }
    public string? PendingLocalFingerprint { get; private set; }

    public static CalendarSyncLink Create(Guid connectionId, CalendarItemKind kind, Guid? localId, Guid? caseId, string remoteId) =>
        new() { Id = Guid.CreateVersion7(), ConnectionId = connectionId, Kind = kind, LocalId = localId, LegalCaseId = caseId, RemoteId = remoteId };

    public void Remember(string version, string snapshot, string localFingerprint, string? remoteId = null)
    {
        if (remoteId is not null) { RemoteId = remoteId; }
        RemoteVersion = version; RemoteSnapshot = snapshot; LocalFingerprint = localFingerprint;
        PendingReason = null; PendingSnapshot = null; PendingLocalFingerprint = null;
    }

    public void Pend(CalendarPendingReason reason, string snapshot, string localFingerprint)
    { PendingReason = reason; PendingSnapshot = snapshot; PendingLocalFingerprint = localFingerprint; }
}

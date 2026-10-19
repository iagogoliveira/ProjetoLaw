using LexFlow.SharedKernel.Domain;

namespace LexFlow.Platform.Domain.Notifications;

public sealed class Notification : Entity<Guid>, ITenantOwned, ICaseScoped
{
    private Notification() { }
    public Guid OrganizationId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Kind { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public Guid? CaseId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Notification Create(string key, string kind, Guid resourceId, Guid? caseId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (key.Length > 200 || kind.Length > 100 || resourceId == Guid.Empty) { throw new ArgumentException("Dados da notificação inválidos.", nameof(key)); }
        return new Notification { Id = Guid.CreateVersion7(), Key = key, Kind = kind, ResourceId = resourceId, CaseId = caseId, CreatedAt = now.ToUniversalTime() };
    }
}
public sealed class NotificationReceipt : Entity<Guid>, ITenantOwned, ICaseScoped
{
    private NotificationReceipt() { }
    public Guid OrganizationId { get; private set; }
    public Guid NotificationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? CaseId { get; private set; }
    public DateTimeOffset ReadAt { get; private set; }
    public static NotificationReceipt Read(Guid notificationId, Guid userId, Guid? caseId, DateTimeOffset now)
    {
        if (notificationId == Guid.Empty || userId == Guid.Empty) { throw new ArgumentException("Notificação e usuário obrigatórios.", nameof(notificationId)); }
        return new NotificationReceipt { Id = Guid.CreateVersion7(), NotificationId = notificationId, UserId = userId, CaseId = caseId, ReadAt = now.ToUniversalTime() };
    }
}

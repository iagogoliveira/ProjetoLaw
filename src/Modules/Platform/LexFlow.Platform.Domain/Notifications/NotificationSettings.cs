using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Domain.Notifications;

public static class NotificationKinds
{
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        "publications.received", "publications.capture_missing", "scheduling.deadline_missed",
        "scheduling.calendar_changed", "scheduling.deadline_due_today",
    });
    public static IReadOnlyList<string> Mandatory { get; } = Array.AsReadOnly(new[]
    {
        "scheduling.deadline_missed", "scheduling.calendar_changed", "scheduling.deadline_due_today",
    });
    public static readonly Error Invalid = Error.Validation("notifications.settings_invalid", "Informe somente tipos de notificação disponíveis, sem duplicação.");

    public static bool Valid(IReadOnlyList<string>? kinds) => kinds is not null && kinds.Count <= All.Count
        && kinds.Distinct(StringComparer.Ordinal).Count() == kinds.Count && kinds.All(k => All.Contains(k, StringComparer.Ordinal));
}

public sealed class NotificationPreference : Entity<Guid>, ITenantOwned, IAuditable
{
    private NotificationPreference() { }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public bool DailyDigestEnabled { get; private set; }
    public bool IncludeAgenda { get; private set; } = true;
    public bool IncludeDeadlines { get; private set; } = true;
    public bool IncludePublications { get; private set; } = true;
    public string[] MutedKinds { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static NotificationPreference Create(Guid userId) => userId == Guid.Empty
        ? throw new ArgumentException("Usuário obrigatório.", nameof(userId))
        : new() { Id = Guid.CreateVersion7(), UserId = userId };

    public Result Set(bool enabled, bool agenda, bool deadlines, bool publications, IReadOnlyList<string>? muted)
    {
        if (!NotificationKinds.Valid(muted)) { return NotificationKinds.Invalid; }
        DailyDigestEnabled = enabled;
        IncludeAgenda = agenda;
        IncludeDeadlines = deadlines;
        IncludePublications = publications;
        MutedKinds = muted!.ToArray();
        return Result.Success;
    }
}

public sealed class NotificationRule : Entity<Guid>, ITenantOwned, IAuditable
{
    private NotificationRule() { }
    public Guid OrganizationId { get; private set; }
    public string[] RequiredKinds { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static NotificationRule Create() => new() { Id = Guid.CreateVersion7() };
    public Result Set(IReadOnlyList<string>? required)
    {
        if (!NotificationKinds.Valid(required)) { return NotificationKinds.Invalid; }
        RequiredKinds = required!.Union(NotificationKinds.Mandatory, StringComparer.Ordinal).ToArray();
        return Result.Success;
    }
}

[NotAudited]
public sealed class DigestDelivery : Entity<Guid>, ITenantOwned
{
    private DigestDelivery() { }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly Day { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public static DigestDelivery Create(Guid userId, DateOnly day) => new() { Id = Guid.CreateVersion7(), UserId = userId, Day = day };
    public void MarkSent(DateTimeOffset now) => SentAt = now.ToUniversalTime();
}

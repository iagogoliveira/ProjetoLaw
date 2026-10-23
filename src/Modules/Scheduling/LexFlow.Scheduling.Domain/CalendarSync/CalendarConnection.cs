using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain.CalendarSync;

public enum CalendarProvider { Google, Microsoft365 }
public enum CalendarItemKind { Event, Deadline }
public enum CalendarPendingReason { Conflict, ExternalDeletion, Unsupported }
public enum CalendarResolution { KeepLocal, AcceptExternal }

public sealed class CalendarConnection : Entity<Guid>, ITenantOwned, IAuditable
{
    private CalendarConnection() { }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public CalendarProvider Provider { get; private set; }
    public DateOnly From { get; private set; }
    public DateOnly Until { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Result<CalendarConnection> Connect(Guid userId, CalendarProvider provider, DateOnly from, DateOnly until)
    {
        if (userId == Guid.Empty || !Enum.IsDefined(provider) || from == default || until.DayNumber - from.DayNumber is < 1 or > 366)
        { return CalendarSyncErrors.InvalidConnection; }
        return new CalendarConnection { Id = Guid.CreateVersion7(), UserId = userId, Provider = provider, From = from, Until = until, IsActive = true };
    }

    public Result Activate(DateOnly from, DateOnly until)
    {
        if (from == default || until.DayNumber - from.DayNumber is < 1 or > 366) { return CalendarSyncErrors.InvalidConnection; }
        From = from; Until = until; IsActive = true; LastErrorCode = null;
        return Result.Success;
    }

    public void Disconnect() => IsActive = false;
    public void Succeeded(DateTimeOffset now) { LastSyncedAt = now.ToUniversalTime(); LastErrorCode = null; }
    public void Failed(string code) => LastErrorCode = code;
}

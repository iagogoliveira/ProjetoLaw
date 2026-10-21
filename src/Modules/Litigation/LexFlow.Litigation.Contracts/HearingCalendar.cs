namespace LexFlow.Litigation.Contracts;

public sealed record HearingCalendarWindow(DateTimeOffset From, DateTimeOffset Until, IReadOnlyCollection<Guid>? Users,
    bool IncludeClosed, DateTimeOffset? AfterAt, Guid? AfterId, bool IncludeEqual, int Limit);
public sealed record HearingCalendarItem(Guid Id, Guid CaseId, DateTimeOffset ScheduledAt, Guid? LawyerUserId, string Type, string Status);

/// <summary>Read-only agenda projection; applies cases.read and ethical walls inside Litigation.</summary>
public interface IHearingCalendar
{
    Task<IReadOnlyList<HearingCalendarItem>> ListAsync(HearingCalendarWindow window, CancellationToken cancellationToken);
}

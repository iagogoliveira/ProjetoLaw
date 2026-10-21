using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain;

public sealed record CalendarEventInput(Guid? CaseId, string Title, string? Description, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, Guid AssigneeUserId, string? Location);
public enum CalendarEventState { Scheduled, Cancelled }

public sealed class CalendarEvent : Entity<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    private CalendarEvent() { }
    public Guid OrganizationId { get; private set; }
    public Guid? LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public string Title { get; private set; } = string.Empty;
    [Sensitive]
    public string? Description { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public Guid AssigneeUserId { get; private set; }
    public string? Location { get; private set; }
    public CalendarEventState Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Result<CalendarEvent> Create(CalendarEventInput input)
    {
        if (!Valid(input)) { return SchedulingErrors.InvalidEvent; }
        var result = new CalendarEvent { Id = Guid.CreateVersion7(), LegalCaseId = input.CaseId };
        result.Apply(input);
        return result;
    }

    public Result Edit(CalendarEventInput input)
    {
        if (Status != CalendarEventState.Scheduled) { return SchedulingErrors.EventClosed; }
        if (!Valid(input) || input.CaseId != LegalCaseId) { return SchedulingErrors.InvalidEvent; }
        Apply(input);
        return Result.Success;
    }

    public Result Cancel()
    {
        if (Status != CalendarEventState.Scheduled) { return SchedulingErrors.EventClosed; }
        Status = CalendarEventState.Cancelled;
        return Result.Success;
    }

    public Result Reassign(Guid userId)
    {
        if (Status != CalendarEventState.Scheduled) { return SchedulingErrors.EventClosed; }
        if (userId == Guid.Empty) { return SchedulingErrors.AssigneeUnavailable; }
        AssigneeUserId = userId;
        return Result.Success;
    }

    private void Apply(CalendarEventInput input)
    {
        Title = input.Title.Trim(); Description = input.Description?.Trim(); StartsAt = input.StartsAt.ToUniversalTime();
        EndsAt = input.EndsAt.ToUniversalTime(); AssigneeUserId = input.AssigneeUserId; Location = input.Location?.Trim();
    }

    private static bool Valid(CalendarEventInput input) => input.CaseId != Guid.Empty && !string.IsNullOrWhiteSpace(input.Title)
        && input.Title.Trim().Length <= 300 && (input.Description?.Length ?? 0) <= 4000 && (input.Location?.Length ?? 0) <= 500
        && input.StartsAt != default && input.EndsAt > input.StartsAt && input.AssigneeUserId != Guid.Empty;
}

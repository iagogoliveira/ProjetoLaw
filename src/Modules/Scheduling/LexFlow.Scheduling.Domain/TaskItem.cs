using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain;

public sealed class TaskItem : Entity<Guid>, ITenantOwned, ICaseScoped, IAuditable
{
    private TaskItem() { }
    public Guid OrganizationId { get; private set; }
    public Guid LegalCaseId { get; private set; }
    Guid? ICaseScoped.CaseId => LegalCaseId;
    public Guid PublicationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly DueOn { get; private set; }
    public Guid AssigneeUserId { get; private set; }
    public Guid ConfirmedBy { get; private set; }
    public DateTimeOffset ConfirmedAt { get; private set; }
    public WorkItemStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Result<TaskItem> Create(Guid caseId, Guid publicationId, string title, DateOnly dueOn,
        Guid assignee, bool confirmed, Guid userId, DateTimeOffset now)
    {
        if (!WorkItemRules.Valid(caseId, publicationId, title, assignee, confirmed, userId) || dueOn == default)
        {
            return SchedulingErrors.InvalidTask;
        }

        return new TaskItem
        {
            Id = Guid.CreateVersion7(),
            LegalCaseId = caseId,
            PublicationId = publicationId,
            Title = title.Trim(),
            DueOn = dueOn,
            AssigneeUserId = assignee,
            ConfirmedBy = userId,
            ConfirmedAt = now.ToUniversalTime(),
        };
    }
}

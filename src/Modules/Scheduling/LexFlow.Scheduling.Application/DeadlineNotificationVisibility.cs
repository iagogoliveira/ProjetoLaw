using LexFlow.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

/// <summary>Visibilidade de alertas de prazo para o feed de notificações e para outros canais.</summary>
internal sealed class DeadlineNotificationVisibility(
    ISchedulingDbContext db,
    ICaseDirectory cases,
    IOrganizationAccessDirectory directory, IClock clock) : INotificationVisibility
{
    private const string DeadlineMissed = "scheduling.deadline_missed";
    private const string CalendarChanged = "scheduling.calendar_changed";
    private const string DueToday = "scheduling.deadline_due_today";
    private const string CasesRead = "cases.read";
    private const string DeadlinesRead = "schedule.deadlines.read";
    private const string DeadlinesVerify = "schedule.deadlines.verify";

    public async Task<IReadOnlyCollection<Guid>> VisibleAsync(
        NotificationViewer viewer,
        IReadOnlyList<NotificationCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var alertCandidates = candidates.Where(IsSupported).ToList();
        if (alertCandidates.Count == 0)
        {
            return [];
        }

        var activityIds = alertCandidates.Select(c => c.ResourceId).Distinct().ToList();
        var alerts = await (from activity in db.DeadlineActivities.AsNoTracking()
                            join deadline in db.Deadlines.AsNoTracking() on activity.DeadlineId equals deadline.Id
                            where activityIds.Contains(activity.Id)
                            select new DeadlineAlertDetails(activity.Id, deadline.Id, deadline.LegalCaseId, activity.Kind,
                                deadline.AssigneeUserId, deadline.ReviewedBy))
            .ToListAsync(cancellationToken);

        var visibleActivities = new HashSet<Guid>();
        foreach (var alert in alerts)
        {
            if (await VisibleToAsync(viewer, alert, cancellationToken))
            {
                visibleActivities.Add(alert.ActivityId);
            }
        }

        var dueIds = candidates.Where(c => string.Equals(c.Kind, DueToday, StringComparison.Ordinal)).Select(c => c.ResourceId).ToArray();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, clock.LegalTimeZone).DateTime);
        var due = await db.Deadlines.AsNoTracking().Where(d => dueIds.Contains(d.Id) && d.FatalDueOn == today
            && d.Status != WorkItemStatus.Completed && d.Status != WorkItemStatus.Missed).ToListAsync(cancellationToken);
        foreach (var deadline in due)
        {
            var detail = new DeadlineAlertDetails(deadline.Id, deadline.Id, deadline.LegalCaseId, DueToday, deadline.AssigneeUserId, null);
            if (await VisibleToAsync(viewer, detail, cancellationToken)) { visibleActivities.Add(deadline.Id); }
        }

        return alertCandidates.Where(c => visibleActivities.Contains(c.ResourceId)).Select(c => c.Id).ToList();
    }

    internal async Task<bool> VisibleToAsync(
        NotificationViewer viewer,
        DeadlineAlertDetails alert,
        CancellationToken cancellationToken)
    {
        if (viewer.UserId == Guid.Empty || !IsSupported(alert.ActivityKind))
        {
            return false;
        }

        var access = await directory.GetAsync(viewer.UserId, cancellationToken);
        if (access is null || (access.MfaRequired && !viewer.MultiFactorAuthenticated)
            || !access.Permissions.ContainsKey(CasesRead) || !access.Permissions.ContainsKey(DeadlinesRead))
        {
            return false;
        }

        var caseRead = await cases.ScopeForUserAsync(viewer.UserId, CasesRead, cancellationToken);
        if (!caseRead.VisibleIds.Contains(alert.CaseId))
        {
            return false;
        }

        var deadlineRead = await cases.ScopeForUserAsync(viewer.UserId, DeadlinesRead, cancellationToken);
        if (!deadlineRead.VisibleIds.Contains(alert.CaseId))
        {
            return false;
        }

        if (viewer.UserId == alert.AssigneeUserId || viewer.UserId == alert.ReviewedBy)
        {
            return true;
        }

        if (!access.Permissions.ContainsKey(DeadlinesVerify))
        {
            return false;
        }

        var verify = await cases.ScopeForUserAsync(viewer.UserId, DeadlinesVerify, cancellationToken);
        return verify.VisibleIds.Contains(alert.CaseId);
    }

    private static bool IsSupported(NotificationCandidate candidate) =>
        IsSupported(candidate.Kind);

    private static bool IsSupported(string kind) =>
        string.Equals(kind, DeadlineMissed, StringComparison.Ordinal)
        || string.Equals(kind, CalendarChanged, StringComparison.Ordinal)
        || string.Equals(kind, DueToday, StringComparison.Ordinal);
}

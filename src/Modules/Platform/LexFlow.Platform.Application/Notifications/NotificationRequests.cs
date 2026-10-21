using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Platform.Domain.Notifications;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Notifications;

public sealed record NotificationPosition(DateTimeOffset CreatedAt, Guid Id);
public sealed record NotificationResponse(Guid Id, string Kind, Guid ResourceId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, Guid? CaseId = null);
public sealed record NotificationsPage(IReadOnlyList<NotificationResponse> Items, NotificationPosition? Next);
public sealed record ListNotificationsQuery(NotificationViewer Viewer, NotificationPosition? After, int Limit, bool UnreadOnly = false) : IQuery<NotificationsPage>;
public sealed record ReadNotificationCommand(Guid Id, NotificationViewer Viewer) : ICommand;
public interface INotificationReadLock
{
    Task AcquireAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken);
}
internal sealed class ListNotificationsValidator : AbstractValidator<ListNotificationsQuery>
{
    public ListNotificationsValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}
internal sealed class NotificationAccess(IOrganizationAccessDirectory directory, IEnumerable<INotificationVisibility> policies, NotificationDeliveryPolicy delivery)
{
    public async Task<IReadOnlyCollection<Guid>> VisibleAsync(NotificationViewer viewer, IReadOnlyList<Notification> rows, CancellationToken cancellationToken)
    {
        var access = await directory.GetAsync(viewer.UserId, cancellationToken);
        if (access is null || (access.MfaRequired && !viewer.MultiFactorAuthenticated)) { return []; }
        var suppressed = await delivery.SuppressedAsync(viewer.UserId, cancellationToken);
        var candidates = rows.Where(n => !suppressed.Contains(n.Kind)).Select(n => new NotificationCandidate(n.Id, n.Kind, n.ResourceId)).ToList();
        var ids = new HashSet<Guid>();
        foreach (var policy in policies) { ids.UnionWith(await policy.VisibleAsync(viewer, candidates, cancellationToken)); }
        return ids;
    }
}
internal sealed class ListNotificationsHandler(IPlatformDbContext db, NotificationAccess access) : IQueryHandler<ListNotificationsQuery, NotificationsPage>
{
    public async Task<Result<NotificationsPage>> Handle(ListNotificationsQuery query, CancellationToken cancellationToken)
    {
        var found = new List<NotificationResponse>();
        var after = query.After;
        while (found.Count <= query.Limit)
        {
            var rows = db.Notifications.AsNoTracking();
            if (query.UnreadOnly) { rows = rows.Where(n => !db.NotificationReceipts.Any(r => r.NotificationId == n.Id && r.UserId == query.Viewer.UserId)); }
            if (after is { } position) { rows = rows.Where(n => n.CreatedAt < position.CreatedAt || (n.CreatedAt == position.CreatedAt && n.Id.CompareTo(position.Id) < 0)); }
            var batch = await rows.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(200).ToListAsync(cancellationToken);
            if (batch.Count == 0) { break; }
            var visible = await access.VisibleAsync(query.Viewer, batch, cancellationToken);
            var reads = await db.NotificationReceipts.AsNoTracking().Where(r => visible.Contains(r.NotificationId) && r.UserId == query.Viewer.UserId)
                .ToDictionaryAsync(r => r.NotificationId, r => r.ReadAt, cancellationToken);
            found.AddRange(batch.Where(n => visible.Contains(n.Id)).Select(n => new NotificationResponse(n.Id, n.Kind, n.ResourceId, n.CreatedAt,
                reads.TryGetValue(n.Id, out var readAt) ? readAt : null, n.CaseId)));
            if (batch.Count < 200) { break; }
            after = new NotificationPosition(batch[^1].CreatedAt, batch[^1].Id);
        }
        var items = found.Take(query.Limit).ToList();
        return new NotificationsPage(items, found.Count > query.Limit ? new NotificationPosition(items[^1].CreatedAt, items[^1].Id) : null);
    }
}
internal sealed class ReadNotificationHandler(IPlatformDbContext db, NotificationAccess access, INotificationReadLock mutex, IClock clock)
    : ICommandHandler<ReadNotificationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ReadNotificationCommand command, CancellationToken cancellationToken)
    {
        await mutex.AcquireAsync(command.Id, command.Viewer.UserId, cancellationToken);
        var notification = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(n => n.Id == command.Id, cancellationToken);
        if (notification is null || !(await access.VisibleAsync(command.Viewer, [notification], cancellationToken)).Contains(notification.Id))
        {
            return Error.NotFound("notifications.not_found", "Notificação não encontrada.");
        }
        if (!await db.NotificationReceipts.AnyAsync(r => r.NotificationId == command.Id && r.UserId == command.Viewer.UserId, cancellationToken))
        {
            db.NotificationReceipts.Add(NotificationReceipt.Read(command.Id, command.Viewer.UserId, notification.CaseId, clock.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
        }
        return Unit.Value;
    }
}

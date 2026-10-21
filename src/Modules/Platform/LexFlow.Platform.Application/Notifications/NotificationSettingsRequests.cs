using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Platform.Domain.Notifications;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Notifications;

public sealed record NotificationSettingsResponse(bool DailyDigestEnabled, bool IncludeAgenda, bool IncludeDeadlines,
    bool IncludePublications, IReadOnlyList<string> MutedKinds, IReadOnlyList<string> RequiredKinds, IReadOnlyList<string> AvailableKinds);
public sealed record NotificationRulesResponse(IReadOnlyList<string> RequiredKinds, IReadOnlyList<string> MandatoryKinds);
public sealed record GetNotificationSettingsQuery(NotificationViewer Viewer) : IQuery<NotificationSettingsResponse>;
public sealed record SetNotificationSettingsCommand(NotificationViewer Viewer, bool DailyDigestEnabled, bool IncludeAgenda,
    bool IncludeDeadlines, bool IncludePublications, IReadOnlyList<string>? MutedKinds) : ICommand<NotificationSettingsResponse>;
public sealed record GetNotificationRulesQuery : IQuery<NotificationRulesResponse>;
public sealed record SetNotificationRulesCommand(IReadOnlyList<string>? RequiredKinds) : ICommand<NotificationRulesResponse>;

internal sealed class NotificationDeliveryPolicy(IPlatformDbContext db)
{
    public async Task<IReadOnlyList<string>> RequiredAsync(CancellationToken ct) =>
        (await db.NotificationRules.AsNoTracking().Select(r => r.RequiredKinds).SingleOrDefaultAsync(ct) ?? [])
        .Union(NotificationKinds.Mandatory, StringComparer.Ordinal).ToArray();

    public async Task<HashSet<string>> SuppressedAsync(Guid userId, CancellationToken ct)
    {
        var muted = await db.NotificationPreferences.AsNoTracking().Where(p => p.UserId == userId)
            .Select(p => p.MutedKinds).SingleOrDefaultAsync(ct) ?? [];
        return muted.Except(await RequiredAsync(ct), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
    }
}

internal sealed class NotificationSettingsAccess(IPlatformDbContext db, IOrganizationAccessDirectory directory, NotificationDeliveryPolicy policy)
{
    public async Task<bool> AllowedAsync(NotificationViewer viewer, CancellationToken ct)
    {
        var member = await directory.GetAsync(viewer.UserId, ct);
        return member is not null && (!member.MfaRequired || viewer.MultiFactorAuthenticated);
    }

    public async Task<NotificationSettingsResponse> ReadAsync(Guid userId, CancellationToken ct)
    {
        var row = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        return new(row?.DailyDigestEnabled ?? false, row?.IncludeAgenda ?? true, row?.IncludeDeadlines ?? true,
            row?.IncludePublications ?? true, row?.MutedKinds ?? [], await policy.RequiredAsync(ct), NotificationKinds.All);
    }

    public static readonly Error Denied = Error.Forbidden("notifications.membership_required", "É necessário membro ativo e autenticação exigida pelo escritório.");
}

internal sealed class GetNotificationSettingsHandler(NotificationSettingsAccess access)
    : IQueryHandler<GetNotificationSettingsQuery, NotificationSettingsResponse>
{
    public async Task<Result<NotificationSettingsResponse>> Handle(GetNotificationSettingsQuery query, CancellationToken ct) =>
        await access.AllowedAsync(query.Viewer, ct) ? await access.ReadAsync(query.Viewer.UserId, ct) : NotificationSettingsAccess.Denied;
}

internal sealed class SetNotificationSettingsHandler(IPlatformDbContext db, NotificationSettingsAccess access, INotificationReadLock mutex)
    : ICommandHandler<SetNotificationSettingsCommand, NotificationSettingsResponse>
{
    public async Task<Result<NotificationSettingsResponse>> Handle(SetNotificationSettingsCommand command, CancellationToken ct)
    {
        if (!await access.AllowedAsync(command.Viewer, ct)) { return NotificationSettingsAccess.Denied; }
        await mutex.AcquireAsync(command.Viewer.UserId, command.Viewer.UserId, ct);
        var row = await db.NotificationPreferences.SingleOrDefaultAsync(p => p.UserId == command.Viewer.UserId, ct);
        var fresh = row is null;
        row ??= NotificationPreference.Create(command.Viewer.UserId);
        var set = row.Set(command.DailyDigestEnabled, command.IncludeAgenda, command.IncludeDeadlines, command.IncludePublications, command.MutedKinds);
        if (set.IsFailure) { return set.Error; }
        if (fresh) { db.NotificationPreferences.Add(row); }
        await db.SaveChangesAsync(ct);
        return await access.ReadAsync(command.Viewer.UserId, ct);
    }
}

internal sealed class GetNotificationRulesHandler(NotificationDeliveryPolicy policy) : IQueryHandler<GetNotificationRulesQuery, NotificationRulesResponse>
{
    public async Task<Result<NotificationRulesResponse>> Handle(GetNotificationRulesQuery query, CancellationToken ct) =>
        new NotificationRulesResponse(await policy.RequiredAsync(ct), NotificationKinds.Mandatory);
}

internal sealed class SetNotificationRulesHandler(IPlatformDbContext db, NotificationDeliveryPolicy policy, INotificationReadLock mutex)
    : ICommandHandler<SetNotificationRulesCommand, NotificationRulesResponse>
{
    public async Task<Result<NotificationRulesResponse>> Handle(SetNotificationRulesCommand command, CancellationToken ct)
    {
        await mutex.AcquireAsync(Guid.Empty, Guid.Empty, ct);
        var row = await db.NotificationRules.SingleOrDefaultAsync(ct);
        var fresh = row is null;
        row ??= NotificationRule.Create();
        var set = row.Set(command.RequiredKinds);
        if (set.IsFailure) { return set.Error; }
        if (fresh) { db.NotificationRules.Add(row); }
        await db.SaveChangesAsync(ct);
        return new NotificationRulesResponse(await policy.RequiredAsync(ct), NotificationKinds.Mandatory);
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Notifications;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Platform.Endpoints;

public sealed record NotificationSettingsInput(bool DailyDigestEnabled, bool IncludeAgenda, bool IncludeDeadlines,
    bool IncludePublications, string[]? MutedKinds);
public sealed record NotificationRulesInput(string[]? RequiredKinds);

internal static class NotificationSettingsEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Notifications");
        group.MapGet("/me/notification-settings", async (HttpContext http, ICurrentUser user,
            IQueryHandler<GetNotificationSettingsQuery, NotificationSettingsResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new(NotificationEndpoints.Viewer(http, user)), ct)).ToHttpResult())
            .RequirePermission(PlatformPermissions.ProfileRead).WithName("GetNotificationSettings").Produces<NotificationSettingsResponse>();
        group.MapPut("/me/notification-settings", async (NotificationSettingsInput input, HttpContext http, ICurrentUser user,
            ICommandHandler<SetNotificationSettingsCommand, NotificationSettingsResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new(NotificationEndpoints.Viewer(http, user), input.DailyDigestEnabled, input.IncludeAgenda,
                input.IncludeDeadlines, input.IncludePublications, input.MutedKinds), ct)).ToHttpResult())
            .RequirePermission(PlatformPermissions.ProfileManage).WithName("SetNotificationSettings").Produces<NotificationSettingsResponse>();
        group.MapGet("/organization/notification-rules", async (IQueryHandler<GetNotificationRulesQuery, NotificationRulesResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new(), ct)).ToHttpResult()).RequirePermission("settings.organization.manage")
            .WithName("GetNotificationRules").Produces<NotificationRulesResponse>();
        group.MapPut("/organization/notification-rules", async (NotificationRulesInput input,
            ICommandHandler<SetNotificationRulesCommand, NotificationRulesResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new(input.RequiredKinds), ct)).ToHttpResult()).RequirePermission("settings.organization.manage")
            .WithName("SetNotificationRules").Produces<NotificationRulesResponse>();
    }
}

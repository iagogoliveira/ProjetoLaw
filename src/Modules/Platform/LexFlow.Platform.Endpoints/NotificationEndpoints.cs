using System.Runtime.CompilerServices;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Platform.Application.Notifications;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Pagination;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Platform.Endpoints;

public sealed record NotificationsPageResponse(IReadOnlyList<NotificationResponse> Items, string? NextCursor);

internal static class NotificationEndpoints
{
    public static NotificationViewer Viewer(HttpContext http, ICurrentUser user) =>
        new(user.UserId ?? Guid.Empty, http.User.FindAll(LexFlowClaims.AuthenticationMethods).Any(c => string.Equals(c.Value, "mfa", StringComparison.Ordinal)));

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Notifications");
        group.MapGet("/notifications", async (string? cursor, int? limit, bool? unreadOnly, HttpContext http, ICurrentUser user,
            IQueryHandler<ListNotificationsQuery, NotificationsPage> handler, CancellationToken ct) =>
        {
            var position = cursor is null ? Result.Ok<NotificationPosition?>(null) : CursorCodec.Decode<NotificationPosition>(cursor).Map<NotificationPosition?>(p => p);
            if (position.IsFailure) { return position.Error.ToProblem(); }
            return (await handler.Handle(new(Viewer(http, user), position.Value, limit ?? 50, unreadOnly ?? false), ct))
                .ToHttpResult(p => TypedResults.Ok(new NotificationsPageResponse(p.Items, p.Next is null ? null : CursorCodec.Encode(p.Next))));
        }).RequirePermission(PlatformPermissions.ProfileRead).WithName("ListNotifications").WithSummary("Notificações visíveis do usuário no escritório ativo").Produces<NotificationsPageResponse>();

        group.MapPost("/notifications/{id:guid}/read", async (Guid id, HttpContext http, ICurrentUser user,
            ICommandHandler<ReadNotificationCommand, Unit> handler, CancellationToken ct) =>
            (await handler.Handle(new(id, Viewer(http, user)), ct)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(PlatformPermissions.ProfileRead).WithName("ReadNotification").WithSummary("Marca a notificação como lida somente para o usuário atual").Produces(StatusCodes.Status204NoContent);

        group.MapHub<NotificationsHub>("/notifications/hub", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.CloseOnAuthenticationExpiration = true;
        }).RequirePermission(PlatformPermissions.ProfileRead);
    }
}

/// <summary>Stream SignalR com leitura persistida e autorização renovada a cada consulta; sem grupos globais nem conteúdo em broadcast.</summary>
public sealed class NotificationsHub(IServiceScopeFactory scopes) : Hub
{
    private const string OfficeKey = "lexflow.notifications.office";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext() ?? throw new HubException("notifications.access_denied");
        var userId = Guid.TryParse(Context.User?.FindFirst(LexFlowClaims.Subject)?.Value, out var id) ? id : throw new HubException("notifications.access_denied");
        await using var scope = scopes.CreateAsyncScope();
        var memberships = await scope.ServiceProvider.GetRequiredService<IMembershipLookup>().GetAsync(userId, Context.ConnectionAborted);
        var requested = http.Request.Headers[TenantResolutionMiddleware.HeaderName].FirstOrDefault() ?? Context.User?.FindFirst(LexFlowClaims.OrganizationId)?.Value;
        var office = requested is null ? memberships.DefaultOrganization : Guid.TryParse(requested, out var selected) ? selected : (Guid?)null;
        if (office is null || !memberships.OrganizationIds.Contains(office.Value)) { throw new HubException("organization.not_a_member"); }
        Context.Items[OfficeKey] = office.Value;
        await base.OnConnectedAsync();
    }

    public async IAsyncEnumerable<NotificationsPageResponse> Watch([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var userId = Guid.TryParse(Context.User?.FindFirst(LexFlowClaims.Subject)?.Value, out var id) ? id : throw new HubException("notifications.access_denied");
        var office = Context.Items.TryGetValue(OfficeKey, out var selected) && selected is Guid value ? value : throw new HubException("notifications.organization_required");
        var mfa = Context.User!.FindAll(LexFlowClaims.AuthenticationMethods).Any(c => string.Equals(c.Value, "mfa", StringComparison.Ordinal));
        string? previous = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var memberships = await scope.ServiceProvider.GetRequiredService<IMembershipLookup>().GetAsync(userId, cancellationToken);
            if (!memberships.OrganizationIds.Contains(office)) { throw new HubException("organization.not_a_member"); }
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForValidatedMembership(office);
            var result = await scope.ServiceProvider.GetRequiredService<IQueryHandler<ListNotificationsQuery, NotificationsPage>>()
                .Handle(new(new(userId, mfa), null, 50, true), cancellationToken);
            if (result.IsFailure) { throw new HubException(result.Error.Code); }
            var key = string.Join(',', result.Value.Items.Select(n => n.Id.ToString("N")));
            if (!string.Equals(previous, key, StringComparison.Ordinal))
            {
                previous = key;
                yield return new NotificationsPageResponse(result.Value.Items, result.Value.Next is null ? null : CursorCodec.Encode(result.Value.Next));
            }
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }
}

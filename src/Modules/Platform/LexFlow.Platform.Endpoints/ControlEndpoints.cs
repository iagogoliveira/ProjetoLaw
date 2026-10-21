using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Control;
using LexFlow.Platform.Contracts.Control;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Platform.Endpoints;

internal static class ControlEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/schedule/control", async (DateOnly? from, DateOnly? until, int? idleDays, int? limit,
            HttpContext http, ICurrentUser user, IQueryHandler<GetControlReportQuery, ControlReportResponse> handler, CancellationToken ct) =>
        {
            var viewer = NotificationEndpoints.Viewer(http, user);
            return (await handler.Handle(new GetControlReportQuery(viewer.UserId, viewer.MultiFactorAuthenticated,
                from, until, idleDays ?? 30, limit ?? 50), ct)).ToHttpResult();
        }).RequirePermission(ControlPermissions.Read).WithTags("Controladoria").WithName("GetControlReport")
            .WithSummary("Pendências e indicadores da controladoria no alcance do usuário").Produces<ControlReportResponse>();
    }
}

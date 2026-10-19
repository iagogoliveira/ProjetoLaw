using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Application.Capture;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Publications.Endpoints;

internal static class CaptureEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Publications");
        group.MapGet("/publication-sources/{id:guid}/capture", async (Guid id, HttpContext http, IQueryHandler<GetCaptureStateQuery, CaptureStateResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new(id, HasMfa(http)), ct)).ToHttpResult()).RequirePermission("identity.profile.read")
            .WithName("GetCaptureState").WithSummary("Execuções e alerta da fonte no dia; exige integração ou triagem no escopo do escritório").Produces<CaptureStateResponse>();
        group.MapGet("/publications/capture-alerts", async (DateOnly? day, int? limit, HttpContext http,
            IQueryHandler<ListCaptureAlertsQuery, IReadOnlyList<CaptureAlertResponse>> handler, CancellationToken ct) =>
            (await handler.Handle(new(day, limit ?? 50, HasMfa(http)), ct)).ToHttpResult()).RequirePermission("identity.profile.read")
            .WithName("ListCaptureAlerts").WithSummary("Alertas de captura do dia; exige integração ou triagem no escopo do escritório").Produces<IReadOnlyList<CaptureAlertResponse>>();
    }
    private static bool HasMfa(HttpContext http) => http.User.FindAll(LexFlowClaims.AuthenticationMethods)
        .Any(c => string.Equals(c.Value, "mfa", StringComparison.Ordinal));
}

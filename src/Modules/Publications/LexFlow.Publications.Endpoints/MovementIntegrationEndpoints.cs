using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Application.Movements;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Publications.Endpoints;

internal static class MovementIntegrationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Publications");
        group.MapGet("/movement-integration", async (IQueryHandler<GetMovementIntegrationQuery, MovementIntegrationResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetMovementIntegrationQuery(), ct)).ToHttpResult())
            .RequirePermission("settings.integrations.manage").WithName("GetMovementIntegration").Produces<MovementIntegrationResponse>();
        group.MapPut("/movement-integration", async (MovementIntegrationInput input, ICommandHandler<ConfigureMovementIntegrationCommand, MovementIntegrationResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new ConfigureMovementIntegrationCommand(input), ct)).ToHttpResult())
            .RequirePermission("settings.integrations.manage").WithName("ConfigureMovementIntegration").Produces<MovementIntegrationResponse>();
        group.MapGet("/cases/{id:guid}/movement-sync", async (Guid id, IQueryHandler<GetMovementSyncQuery, MovementSyncResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetMovementSyncQuery(id), ct)).ToHttpResult())
            .RequirePermission("cases.read").WithName("GetMovementSync").Produces<MovementSyncResponse>();
    }
}

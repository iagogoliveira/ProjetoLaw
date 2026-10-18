using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Claims;
using LexFlow.Litigation.Domain.Claims;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Litigation.Endpoints;

/// <summary>Pedidos do processo (RF-13): valores por instância, atualização monetária e histórico das alterações.</summary>
internal static class ClaimEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var claims = endpoints.MapGroup("/api/v1/cases").WithTags("Cases");

        claims.MapGet("/{id:guid}/claims", async (Guid id, DateOnly? updateTo, IQueryHandler<ListCaseClaimsQuery, CaseClaimsResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListCaseClaimsQuery(id, updateTo), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("ListCaseClaims")
            .WithSummary("Pedidos do processo com os totais por instância; com updateTo, os valores corrigidos e com juros até a data")
            .Produces<CaseClaimsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        claims.MapPost("/{id:guid}/claims", async (Guid id, ClaimInput request, ICommandHandler<CreateClaimCommand, ClaimResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateClaimCommand(id, request), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/cases/{id:D}/claims/{created.Id:D}", created)))
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("CreateCaseClaim")
            .WithSummary("Cadastra um pedido com os valores por instância e a forma de correção")
            .Produces<ClaimResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        claims.MapPut("/{id:guid}/claims/{claimId:guid}", async (Guid id, Guid claimId, ClaimInput request, ICommandHandler<UpdateClaimCommand, ClaimResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdateClaimCommand(id, claimId, request), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("UpdateCaseClaim")
            .WithSummary("Substitui os dados do pedido; cada valor alterado entra no histórico")
            .Produces<ClaimResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        claims.MapDelete("/{id:guid}/claims/{claimId:guid}", async (Guid id, Guid claimId, ICommandHandler<DeleteClaimCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeleteClaimCommand(id, claimId), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("DeleteCaseClaim")
            .WithSummary("Exclui (logicamente) um pedido; o histórico de valores fica")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        claims.MapGet("/{id:guid}/claims/{claimId:guid}/history", async (Guid id, Guid claimId, IQueryHandler<GetClaimHistoryQuery, IReadOnlyList<ClaimValueChangeResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetClaimHistoryQuery(id, claimId), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("GetCaseClaimHistory")
            .WithSummary("Histórico das alterações de valor do pedido: anterior, novo, quem e quando")
            .Produces<IReadOnlyList<ClaimValueChangeResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

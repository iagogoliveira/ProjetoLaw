using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Conflicts;
using LexFlow.Crm.Application.Opportunities;
using LexFlow.Crm.Domain.Opportunities;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Pagination;
using LexFlow.Web.Common.Queries;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Crm.Endpoints;

public sealed record MoveOpportunityRequest(OpportunityStage Stage);

public sealed record LoseOpportunityRequest(Guid ReasonId, string? Note);

public sealed record OpportunitiesPageResponse(IReadOnlyList<OpportunitySummary> Items, string? NextCursor);

/// <summary>Funil de oportunidades (RF-08, Etapa 4, PR 4C): cadastro, kanban por etapa, perda e conflito do interessado.</summary>
internal static class OpportunityEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapQueries(endpoints);
        MapCommands(endpoints);
        MapActions(endpoints);
    }

    private static void MapQueries(IEndpointRouteBuilder endpoints)
    {
        var opportunities = endpoints.MapGroup("/api/v1/opportunities").WithTags("Opportunities");

        opportunities.MapGet("/", ListAsync)
            .DocumentEnumQuery<OpportunityStage>("stage")
            .RequirePermission(CrmPermissions.OpportunitiesRead)
            .WithName("ListOpportunities")
            .WithSummary("Cartões do funil, por etapa e responsável, dos mais recentes aos mais antigos")
            .Produces<OpportunitiesPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        opportunities.MapGet("/summary", async (Guid? responsibleId, IQueryHandler<OpportunitiesSummaryQuery, IReadOnlyList<OpportunityStageSummary>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new OpportunitiesSummaryQuery(responsibleId), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.OpportunitiesRead)
            .WithName("SummarizeOpportunities")
            .WithSummary("Quantidade e valor estimado por etapa do funil")
            .Produces<IReadOnlyList<OpportunityStageSummary>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        opportunities.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetOpportunityQuery, OpportunityResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetOpportunityQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.OpportunitiesRead)
            .WithName("GetOpportunity")
            .WithSummary("Uma oportunidade do funil")
            .Produces<OpportunityResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>Lista paginada por cursor (<c>?stage=&amp;responsibleId=&amp;cursor=&amp;limit=</c>).</summary>
    private static async Task<IResult> ListAsync(string? stage, Guid? responsibleId, string? cursor, int? limit, IQueryHandler<ListOpportunitiesQuery, OpportunitiesPage> handler, CancellationToken cancellationToken)
    {
        var page = CursorPageRequest.Create(cursor, limit);
        if (page.IsFailure)
        {
            return page.Error.ToProblem();
        }

        var stageFilter = QueryEnums.Parse<OpportunityStage>(stage);
        if (stageFilter.IsFailure)
        {
            return stageFilter.Error.ToProblem();
        }

        OpportunitiesPosition? after = null;
        if (page.Value.Cursor is { } value)
        {
            var decoded = CursorCodec.Decode<OpportunitiesPosition>(value);
            if (decoded.IsFailure)
            {
                return decoded.Error.ToProblem();
            }

            after = decoded.Value;
        }

        var result = await handler.Handle(new ListOpportunitiesQuery(stageFilter.Value, responsibleId, after, page.Value.Limit), cancellationToken);
        return result.ToHttpResult(p => TypedResults.Ok(new OpportunitiesPageResponse(p.Items, p.Next is null ? null : CursorCodec.Encode(p.Next))));
    }

    private static void MapCommands(IEndpointRouteBuilder endpoints)
    {
        var opportunities = endpoints.MapGroup("/api/v1/opportunities").WithTags("Opportunities");

        opportunities.MapPost("/", async (OpportunityInput request, ICommandHandler<CreateOpportunityCommand, OpportunityResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateOpportunityCommand(request), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/opportunities/{created.Id:D}", created)))
            .RequirePermission(CrmPermissions.OpportunitiesManage)
            .WithName("CreateOpportunity")
            .WithSummary("Abre uma oportunidade em lead, com o interessado (dados soltos ou pessoa cadastrada)")
            .Produces<OpportunityResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        opportunities.MapPut("/{id:guid}", async (Guid id, OpportunityInput request, ICommandHandler<UpdateOpportunityCommand, OpportunityResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdateOpportunityCommand(id, request), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.OpportunitiesManage)
            .WithName("UpdateOpportunity")
            .WithSummary("Substitui os dados da oportunidade; mudar o nome ou o documento do interessado desliga a verificação de conflito")
            .Produces<OpportunityResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        opportunities.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeleteOpportunityCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeleteOpportunityCommand(id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(CrmPermissions.OpportunitiesManage)
            .WithName("DeleteOpportunity")
            .WithSummary("Exclui (logicamente) uma oportunidade que não foi ganha, com as propostas dela")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    }

    private static void MapActions(IEndpointRouteBuilder endpoints)
    {
        var opportunities = endpoints.MapGroup("/api/v1/opportunities").WithTags("Opportunities");

        opportunities.MapPost("/{id:guid}/stage", async (Guid id, MoveOpportunityRequest request, ICommandHandler<MoveOpportunityCommand, OpportunityResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new MoveOpportunityCommand(id, request.Stage), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.OpportunitiesManage)
            .WithName("MoveOpportunity")
            .WithSummary("Move entre lead, reunião e proposta (ou reabre a oportunidade perdida)")
            .Produces<OpportunityResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        opportunities.MapPost("/{id:guid}/lose", async (Guid id, LoseOpportunityRequest request, ICommandHandler<LoseOpportunityCommand, OpportunityResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new LoseOpportunityCommand(id, request.ReasonId, request.Note), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.OpportunitiesManage)
            .WithName("LoseOpportunity")
            .WithSummary("Marca a oportunidade como perdida, com o motivo e uma observação")
            .Produces<OpportunityResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        opportunities.MapPost("/{id:guid}/conflict-check", async (Guid id, ICommandHandler<CheckOpportunityConflictCommand, ConflictCheckResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CheckOpportunityConflictCommand(id), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/conflict-checks/{created.Id:D}", created)))
            .RequirePermission(CrmPermissions.ConflictsCheck)
            .WithName("CheckOpportunityConflict")
            .WithSummary("Verifica conflito com os dados do interessado (lado cliente) e liga a verificação à oportunidade")
            .Produces<ConflictCheckResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}

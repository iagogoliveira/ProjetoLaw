using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Application.OrgStructure;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Organization.Endpoints;

public sealed record BranchRequest(string Name, string? Cnpj, string? MunicipalRegistration);

public sealed record PracticeAreaRequest(string Name);

public sealed record TeamRequest(string Name, Guid? PracticeAreaId, Guid? BranchId);

public sealed record TeamMembersRequest(IReadOnlyList<Guid> MemberIds);

public sealed record CostCenterRequest(string Code, string Name);

public sealed record PartnerRequest(string Name, decimal SharePercentage, Guid? MemberId);

/// <summary>
/// Cadastro da estrutura (seção 3.1, RF-02): filiais, áreas, equipes (e seus membros), centros de custo e sócios da
/// organização ativa. Leitura e escrita exigem <c>settings.structure.manage</c>; a exclusão é lógica e barrada enquanto
/// o item estiver em uso.
/// </summary>
internal static class StructureEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var structure = endpoints.MapGroup("/api/v1/organization").WithTags("Structure");

        MapResource<BranchRequest, SaveBranchCommand, BranchResponse, ListBranchesQuery>(
            structure, "branches", "Branch", "Branches", "filial", StructureKind.Branch,
            (id, r) => new SaveBranchCommand(id, r.Name, r.Cnpj, r.MunicipalRegistration));
        MapResource<PracticeAreaRequest, SavePracticeAreaCommand, PracticeAreaResponse, ListPracticeAreasQuery>(
            structure, "practice-areas", "PracticeArea", "PracticeAreas", "área de atuação", StructureKind.PracticeArea,
            (id, r) => new SavePracticeAreaCommand(id, r.Name));
        MapResource<TeamRequest, SaveTeamCommand, TeamResponse, ListTeamsQuery>(
            structure, "teams", "Team", "Teams", "equipe", StructureKind.Team,
            (id, r) => new SaveTeamCommand(id, r.Name, r.PracticeAreaId, r.BranchId));
        MapResource<CostCenterRequest, SaveCostCenterCommand, CostCenterResponse, ListCostCentersQuery>(
            structure, "cost-centers", "CostCenter", "CostCenters", "centro de custo", StructureKind.CostCenter,
            (id, r) => new SaveCostCenterCommand(id, r.Code, r.Name));
        MapResource<PartnerRequest, SavePartnerCommand, PartnerResponse, ListPartnersQuery>(
            structure, "partners", "Partner", "Partners", "sócio", StructureKind.Partner,
            (id, r) => new SavePartnerCommand(id, r.Name, r.SharePercentage, r.MemberId));

        structure.MapPut("/teams/{id:guid}/members", async (Guid id, TeamMembersRequest request, ICommandHandler<SetTeamMembersCommand, TeamResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new SetTeamMembersCommand(id, request.MemberIds), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.SettingsStructureManage)
            .WithName("SetTeamMembers")
            .WithSummary("Substitui os membros de uma equipe")
            .Produces<TeamResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>Listar, criar, editar e excluir um tipo de cadastro, com os nomes de operação do contrato OpenAPI.</summary>
    private static void MapResource<TRequest, TCommand, TResponse, TListQuery>(
        RouteGroupBuilder group,
        string path,
        string singular,
        string plural,
        string label,
        StructureKind kind,
        Func<Guid?, TRequest, TCommand> save)
        where TCommand : ICommand<TResponse>
        where TListQuery : IQuery<IReadOnlyList<TResponse>>, new()
    {
        group.MapGet($"/{path}", async (IQueryHandler<TListQuery, IReadOnlyList<TResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new TListQuery(), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.SettingsStructureManage)
            .WithName($"List{plural}")
            .WithSummary($"Lista: {label}")
            .Produces<IReadOnlyList<TResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost($"/{path}", async (TRequest request, ICommandHandler<TCommand, TResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(save(null, request), cancellationToken)).ToHttpResult(created => TypedResults.Created((string?)null, created)))
            .RequirePermission(PermissionCatalog.SettingsStructureManage)
            .WithName($"Create{singular}")
            .WithSummary($"Cadastra: {label}")
            .Produces<TResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut($"/{path}/{{id:guid}}", async (Guid id, TRequest request, ICommandHandler<TCommand, TResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(save(id, request), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.SettingsStructureManage)
            .WithName($"Update{singular}")
            .WithSummary($"Edita: {label}")
            .Produces<TResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete($"/{path}/{{id:guid}}", async (Guid id, ICommandHandler<DeleteStructureCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeleteStructureCommand(kind, id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(PermissionCatalog.SettingsStructureManage)
            .WithName($"Delete{singular}")
            .WithSummary($"Exclui: {label} (barrado se estiver em uso)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}

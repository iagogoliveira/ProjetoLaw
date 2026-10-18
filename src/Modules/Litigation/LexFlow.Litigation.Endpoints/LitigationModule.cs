using System.Text.Json.Nodes;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Cases;
using LexFlow.Litigation.Application.Numbering;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Modules;
using LexFlow.Web.Common.Pagination;
using LexFlow.Web.Common.Queries;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Litigation.Endpoints;

/// <summary>Permissões usadas pelos endpoints do módulo Litigation (nomes do <c>PermissionCatalog</c>).</summary>
public static class LitigationPermissions
{
    public const string CasesRead = "cases.read";
    public const string CasesCreate = "cases.create";
    public const string CasesEdit = "cases.edit";
    public const string CasesDelete = "cases.delete";
    public const string SettingsOrganizationManage = "settings.organization.manage";
}

/// <summary>Processo ou caso enviado na criação: o tipo (judicial, administrativo, extrajudicial) e os dados.</summary>
public sealed record CreateCaseRequest(
    CaseKind Kind,
    string? CnjNumber,
    Guid? CourtId,
    CaseInstance? Instance,
    string? District,
    string? CourtUnit,
    string? Rite,
    string? ProceduralClass,
    string? Subject,
    Guid? ActionTypeId,
    Guid? PracticeAreaId,
    Guid? BranchId,
    Guid? PhaseId,
    decimal? ClaimValue,
    DateOnly? FiledOn,
    DateOnly? ServedOn,
    string? Description,
    Guid? PayerPersonId,
    IReadOnlyList<PartyInput>? Parties,
    IReadOnlyList<AssignmentInput>? Assignments,
    JsonObject? CustomFields)
{
    public CaseInput ToInput() => new(
        CnjNumber, CourtId, Instance, District, CourtUnit, Rite, ProceduralClass, Subject, ActionTypeId, PracticeAreaId, BranchId,
        PhaseId, ClaimValue, FiledOn, ServedOn, Description, PayerPersonId, Parties, Assignments, CustomFields);
}

public sealed record CaseStatusRequest(CaseStatus Status);

public sealed record LinkCaseRequest(Guid ChildCaseId, RelationKind Kind);

public sealed record NumberingRequest(string Format, IReadOnlyDictionary<Guid, string>? AreaCodes);

public sealed record CasesPageResponse(IReadOnlyList<CaseSummary> Items, string? NextCursor);

/// <summary>
/// Endpoints do módulo Litigation (Etapa 5, PR 5A): processos e casos (judiciais, administrativos e extrajudiciais),
/// partes, responsáveis, hierarquia, encerramento, consulta do número CNJ e a numeração interna do escritório.
/// </summary>
public sealed class LitigationModule : IModule
{
    public static readonly Error InvalidFilter = Error.Validation("litigation.filter_invalid", "Filtro inválido.");

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        MapQueries(endpoints);
        MapCommands(endpoints);
        MapLifecycle(endpoints);
    }

    private static void MapQueries(IEndpointRouteBuilder endpoints)
    {
        var cases = endpoints.MapGroup("/api/v1/cases").WithTags("Cases");

        cases.MapGet("/", ListAsync)
            .DocumentEnumQuery<CaseStatus>("status")
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("ListCases")
            .WithSummary("Processos e casos, dos mais recentes aos mais antigos, com busca pela pasta ou pelo CNJ")
            .Produces<CasesPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        cases.MapGet("/cnj-lookup", async (string number, IQueryHandler<LookupCnjQuery, CnjInfoResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new LookupCnjQuery(number), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("LookupCnj")
            .WithSummary("Confere o número CNJ e diz o ano, o segmento e o tribunal, para pré-preencher o cadastro")
            .Produces<CnjInfoResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        cases.MapGet("/numbering", async (IQueryHandler<GetNumberingQuery, NumberingResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetNumberingQuery(), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("GetCaseNumbering")
            .WithSummary("Formato da numeração interna e as siglas das áreas")
            .Produces<NumberingResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        cases.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetCaseQuery, CaseResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetCaseQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesRead)
            .WithName("GetCase")
            .WithSummary("Um processo ou caso com as partes, os responsáveis e a hierarquia")
            .Produces<CaseResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>Lista paginada por cursor (<c>?q=&amp;status=&amp;practiceAreaId=&amp;cursor=&amp;limit=</c>).</summary>
    private static async Task<IResult> ListAsync(string? q, string? status, Guid? practiceAreaId, string? cursor, int? limit, IQueryHandler<ListCasesQuery, CasesPage> handler, CancellationToken cancellationToken)
    {
        var page = CursorPageRequest.Create(cursor, limit);
        if (page.IsFailure)
        {
            return page.Error.ToProblem();
        }

        var statusFilter = EnumQuery.Parse<CaseStatus>(status, InvalidFilter);
        if (statusFilter.IsFailure)
        {
            return statusFilter.Error.ToProblem();
        }

        CasesPosition? after = null;
        if (page.Value.Cursor is { } value)
        {
            var decoded = CursorCodec.Decode<CasesPosition>(value);
            if (decoded.IsFailure)
            {
                return decoded.Error.ToProblem();
            }

            after = decoded.Value;
        }

        var result = await handler.Handle(new ListCasesQuery(q, statusFilter.Value, practiceAreaId, after, page.Value.Limit), cancellationToken);
        return result.ToHttpResult(p => TypedResults.Ok(new CasesPageResponse(p.Items, p.Next is null ? null : CursorCodec.Encode(p.Next))));
    }

    private static void MapCommands(IEndpointRouteBuilder endpoints)
    {
        var cases = endpoints.MapGroup("/api/v1/cases").WithTags("Cases");

        cases.MapPost("/", async (CreateCaseRequest request, ICommandHandler<CreateCaseCommand, CaseResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateCaseCommand(request.Kind, request.ToInput()), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/cases/{created.Id:D}", created)))
            .RequirePermission(LitigationPermissions.CasesCreate)
            .WithName("CreateCase")
            .WithSummary("Cadastra um processo ou caso; a pasta interna sai da numeração do escritório")
            .Produces<CaseResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        cases.MapPut("/{id:guid}", async (Guid id, CaseInput request, ICommandHandler<UpdateCaseCommand, CaseResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdateCaseCommand(id, request), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("UpdateCase")
            .WithSummary("Substitui os dados do processo ou caso (o tipo e a pasta interna não mudam)")
            .Produces<CaseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        cases.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeleteCaseCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeleteCaseCommand(id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(LitigationPermissions.CasesDelete)
            .WithName("DeleteCase")
            .WithSummary("Exclui (logicamente) um processo ou caso")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        cases.MapPut("/numbering", async (NumberingRequest request, ICommandHandler<ConfigureNumberingCommand, NumberingResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ConfigureNumberingCommand(request.Format, request.AreaCodes), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.SettingsOrganizationManage)
            .WithName("ConfigureCaseNumbering")
            .WithSummary("Troca o formato da numeração interna e as siglas das áreas")
            .Produces<NumberingResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static void MapLifecycle(IEndpointRouteBuilder endpoints)
    {
        var cases = endpoints.MapGroup("/api/v1/cases").WithTags("Cases");

        cases.MapPost("/{id:guid}/close", async (Guid id, ClosingInput request, ICommandHandler<CloseCaseCommand, CaseResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CloseCaseCommand(id, request), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("CloseCase")
            .WithSummary("Encerra com motivo, resultado, valor final e data")
            .Produces<CaseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        cases.MapPost("/{id:guid}/reopen", async (Guid id, ICommandHandler<ChangeCaseStatusCommand, CaseResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ChangeCaseStatusCommand(id, null, Reopen: true), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("ReopenCase")
            .WithSummary("Reabre o caso encerrado")
            .Produces<CaseResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        cases.MapPost("/{id:guid}/status", async (Guid id, CaseStatusRequest request, ICommandHandler<ChangeCaseStatusCommand, CaseResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ChangeCaseStatusCommand(id, request.Status, Reopen: false), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("ChangeCaseStatus")
            .WithSummary("Suspende ou reativa o caso (o encerramento é pela ação própria)")
            .Produces<CaseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        cases.MapPost("/{id:guid}/relations", async (Guid id, LinkCaseRequest request, ICommandHandler<LinkCaseCommand, CaseResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new LinkCaseCommand(id, request.ChildCaseId, request.Kind), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("LinkCase")
            .WithSummary("Liga um incidente, recurso, cumprimento ou apenso a este processo principal")
            .Produces<CaseResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        cases.MapDelete("/{id:guid}/relations/{relationId:guid}", async (Guid id, Guid relationId, ICommandHandler<UnlinkCaseCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UnlinkCaseCommand(id, relationId), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(LitigationPermissions.CasesEdit)
            .WithName("UnlinkCase")
            .WithSummary("Desfaz uma relação da hierarquia")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

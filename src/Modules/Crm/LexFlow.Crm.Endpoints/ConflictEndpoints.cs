using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Conflicts;
using LexFlow.Crm.Application.People;
using LexFlow.Crm.Domain.Conflicts;
using LexFlow.Web.Common.Pagination;
using LexFlow.Web.Common.Queries;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Crm.Endpoints;

public sealed record CheckConflictRequest(string Name, string? Document, ConflictSide Side, IReadOnlyList<string>? ContactEmails);

public sealed record ProceedConflictRequest(string? Justification);

public sealed record MergePeopleRequest(Guid DuplicateId);

public sealed record AnonymizePersonRequest(string Reason);

public sealed record ConflictChecksPageResponse(IReadOnlyList<ConflictCheckResponse> Items, string? NextCursor);

/// <summary>
/// Conflito de interesses (RF-07), fusão de cadastros duplicados e anonimização LGPD (Etapa 4, PR 4B).
/// </summary>
internal static class ConflictEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapConflicts(endpoints);
        MapLifecycle(endpoints);
    }

    private static void MapConflicts(IEndpointRouteBuilder endpoints)
    {
        var checks = endpoints.MapGroup("/api/v1/conflict-checks").WithTags("ConflictChecks");

        checks.MapPost("/", async (CheckConflictRequest request, ICommandHandler<CheckConflictCommand, ConflictCheckResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CheckConflictCommand(request.Name, request.Document, request.Side, request.ContactEmails), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/conflict-checks/{created.Id:D}", created)))
            .RequirePermission(CrmPermissions.ConflictsCheck)
            .WithName("CheckConflict")
            .WithSummary("Verifica conflito de interesses (direto, indireto ou nenhum) e grava a verificação")
            .Produces<ConflictCheckResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        checks.MapGet("/", ListAsync)
            .DocumentEnumQuery<ConflictRisk>("risk")
            .RequirePermission(CrmPermissions.ConflictsCheck)
            .WithName("ListConflictChecks")
            .WithSummary("Relatório das verificações de conflito, das mais recentes às mais antigas")
            .Produces<ConflictChecksPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        checks.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetConflictCheckQuery, ConflictCheckResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetConflictCheckQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.ConflictsCheck)
            .WithName("GetConflictCheck")
            .WithSummary("Uma verificação de conflito com os achados")
            .Produces<ConflictCheckResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        checks.MapPost("/{id:guid}/proceed", async (Guid id, ProceedConflictRequest request, ICommandHandler<ProceedConflictCommand, ConflictCheckResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ProceedConflictCommand(id, request.Justification), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.ConflictsCheck)
            .WithName("ProceedConflict")
            .WithSummary("Segue com o cliente ou caso; com conflito apontado, exige justificativa")
            .Produces<ConflictCheckResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    /// <summary>Relatório paginado por cursor (<c>?risk=&amp;cursor=&amp;limit=</c>).</summary>
    private static async Task<IResult> ListAsync(string? risk, string? cursor, int? limit, IQueryHandler<ListConflictChecksQuery, ConflictChecksPage> handler, CancellationToken cancellationToken)
    {
        var page = CursorPageRequest.Create(cursor, limit);
        if (page.IsFailure)
        {
            return page.Error.ToProblem();
        }

        var riskFilter = QueryEnums.Parse<ConflictRisk>(risk);
        if (riskFilter.IsFailure)
        {
            return riskFilter.Error.ToProblem();
        }

        ConflictChecksPosition? after = null;
        if (page.Value.Cursor is { } value)
        {
            var decoded = CursorCodec.Decode<ConflictChecksPosition>(value);
            if (decoded.IsFailure)
            {
                return decoded.Error.ToProblem();
            }

            after = decoded.Value;
        }

        var result = await handler.Handle(new ListConflictChecksQuery(riskFilter.Value, after, page.Value.Limit), cancellationToken);
        return result.ToHttpResult(p => TypedResults.Ok(new ConflictChecksPageResponse(p.Items, p.Next is null ? null : CursorCodec.Encode(p.Next))));
    }

    private static void MapLifecycle(IEndpointRouteBuilder endpoints)
    {
        var people = endpoints.MapGroup("/api/v1/people").WithTags("People");

        people.MapPost("/{id:guid}/merge", async (Guid id, MergePeopleRequest request, ICommandHandler<MergePeopleCommand, PersonResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new MergePeopleCommand(id, request.DuplicateId), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.PeopleMerge)
            .WithName("MergePeople")
            .WithSummary("Unifica um cadastro duplicado neste: vazios completados, papéis, endereços e contatos somados")
            .Produces<PersonResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        people.MapPost("/{id:guid}/anonymize", async (Guid id, AnonymizePersonRequest request, ICommandHandler<AnonymizePersonCommand, PersonResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new AnonymizePersonCommand(id, request.Reason), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.PeopleAnonymize)
            .WithName("AnonymizePerson")
            .WithSummary("Anonimiza os dados pessoais (LGPD, irreversível), mantendo o cadastro e os vínculos")
            .Produces<PersonResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}

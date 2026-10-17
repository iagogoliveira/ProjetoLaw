using System.Text.Json.Nodes;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Application.People;
using LexFlow.Crm.Domain.People;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Modules;
using LexFlow.Web.Common.Pagination;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Crm.Endpoints;

/// <summary>Permissões usadas pelos endpoints do módulo Crm (nomes do <c>PermissionCatalog</c>).</summary>
public static class CrmPermissions
{
    public const string PeopleRead = "crm.people.read";
    public const string PeopleCreate = "crm.people.create";
    public const string PeopleEdit = "crm.people.edit";
    public const string PeopleDelete = "crm.people.delete";
}

/// <summary>Pessoa enviada na criação: o tipo (física ou jurídica) e o cadastro.</summary>
public sealed record CreatePersonRequest(
    PersonKind Kind,
    string Name,
    string? AlternateName,
    string? Document,
    string? Email,
    string? Phone,
    string? Notes,
    FiscalData? Fiscal,
    Guid? EconomicGroupId,
    IReadOnlyList<PersonRoleKind> Roles,
    IReadOnlyList<AddressInput>? Addresses,
    IReadOnlyList<ContactInput>? Contacts,
    JsonObject? CustomFields)
{
    public PersonInput ToInput() => new(Name, AlternateName, Document, Email, Phone, Notes, Fiscal, EconomicGroupId, Roles, Addresses, Contacts, CustomFields);
}

public sealed record EconomicGroupRequest(string Name);

/// <summary>Página da lista de pessoas; <see cref="NextCursor"/> nulo é a última (ou uma busca por termo).</summary>
public sealed record PeoplePageResponse(IReadOnlyList<PersonSummary> Items, string? NextCursor);

/// <summary>
/// Endpoints do módulo Crm (Etapa 4, PR 4A): cadastro único de pessoas com papéis, endereços, contatos, dados
/// fiscais e campos customizados; busca aproximada; possíveis duplicatas; consulta de CNPJ; grupos econômicos.
/// </summary>
public sealed class CrmModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        MapQueries(endpoints);
        MapCommands(endpoints);
        MapEconomicGroups(endpoints);
    }

    private static void MapQueries(IEndpointRouteBuilder endpoints)
    {
        var people = endpoints.MapGroup("/api/v1/people").WithTags("People");

        people.MapGet("/", async (string? q, PersonRoleKind? role, string? cursor, int? limit, IQueryHandler<SearchPeopleQuery, PeoplePage> handler, CancellationToken cancellationToken) =>
            {
                var page = CursorPageRequest.Create(cursor, limit);
                if (page.IsFailure)
                {
                    return page.Error.ToProblem();
                }

                PeoplePosition? after = null;
                if (page.Value.Cursor is { } value)
                {
                    var decoded = CursorCodec.Decode<PeoplePosition>(value);
                    if (decoded.IsFailure)
                    {
                        return decoded.Error.ToProblem();
                    }

                    after = decoded.Value;
                }

                var result = await handler.Handle(new SearchPeopleQuery(q, role, after, page.Value.Limit), cancellationToken);
                return result.ToHttpResult(p => TypedResults.Ok(new PeoplePageResponse(p.Items, p.Next is null ? null : CursorCodec.Encode(p.Next))));
            })
            .RequirePermission(CrmPermissions.PeopleRead)
            .WithName("ListPeople")
            .WithSummary("Pessoas em ordem alfabética (cursor) ou buscadas por nome aproximado, sem acentos, ou documento")
            .Produces<PeoplePageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        people.MapGet("/similar", async (string? name, string? document, Guid? excludeId, IQueryHandler<FindSimilarPeopleQuery, IReadOnlyList<SimilarPerson>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new FindSimilarPeopleQuery(name, document, excludeId), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.PeopleRead)
            .WithName("FindSimilarPeople")
            .WithSummary("Possíveis duplicatas: mesmo documento ou nome parecido")
            .Produces<IReadOnlyList<SimilarPerson>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        people.MapGet("/cnpj-lookup", async (string cnpj, IQueryHandler<LookupCnpjQuery, CompanyRegistration> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new LookupCnpjQuery(cnpj), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.PeopleCreate)
            .WithName("LookupCnpj")
            .WithSummary("Dados da empresa na Receita Federal (BrasilAPI) para preencher o cadastro")
            .Produces<CompanyRegistration>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        people.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetPersonQuery, PersonResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetPersonQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.PeopleRead)
            .WithName("GetPerson")
            .WithSummary("Cadastro completo de uma pessoa")
            .Produces<PersonResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapCommands(IEndpointRouteBuilder endpoints)
    {
        var people = endpoints.MapGroup("/api/v1/people").WithTags("People");

        people.MapPost("/", async (CreatePersonRequest request, ICommandHandler<CreatePersonCommand, PersonResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreatePersonCommand(request.Kind, request.ToInput()), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/people/{created.Id:D}", created)))
            .RequirePermission(CrmPermissions.PeopleCreate)
            .WithName("CreatePerson")
            .WithSummary("Cadastra uma pessoa; CPF/CNPJ é único no escritório")
            .Produces<PersonResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        people.MapPut("/{id:guid}", async (Guid id, PersonInput request, ICommandHandler<UpdatePersonCommand, PersonResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdatePersonCommand(id, request), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.PeopleEdit)
            .WithName("UpdatePerson")
            .WithSummary("Substitui o cadastro de uma pessoa (o tipo, física ou jurídica, não muda)")
            .Produces<PersonResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        people.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeletePersonCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeletePersonCommand(id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(CrmPermissions.PeopleDelete)
            .WithName("DeletePerson")
            .WithSummary("Exclui (logicamente) uma pessoa")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapEconomicGroups(IEndpointRouteBuilder endpoints)
    {
        var groups = endpoints.MapGroup("/api/v1/economic-groups").WithTags("People");

        groups.MapGet("/", async (IQueryHandler<ListEconomicGroupsQuery, IReadOnlyList<EconomicGroupResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListEconomicGroupsQuery(), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.PeopleRead)
            .WithName("ListEconomicGroups")
            .WithSummary("Grupos econômicos e quantas pessoas cada um tem")
            .Produces<IReadOnlyList<EconomicGroupResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        groups.MapPost("/", async (EconomicGroupRequest request, ICommandHandler<SaveEconomicGroupCommand, EconomicGroupResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new SaveEconomicGroupCommand(null, request.Name), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created((string?)null, created)))
            .RequirePermission(CrmPermissions.PeopleEdit)
            .WithName("CreateEconomicGroup")
            .WithSummary("Cria um grupo econômico")
            .Produces<EconomicGroupResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        groups.MapPut("/{id:guid}", async (Guid id, EconomicGroupRequest request, ICommandHandler<SaveEconomicGroupCommand, EconomicGroupResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new SaveEconomicGroupCommand(id, request.Name), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.PeopleEdit)
            .WithName("RenameEconomicGroup")
            .WithSummary("Renomeia um grupo econômico")
            .Produces<EconomicGroupResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        groups.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeleteEconomicGroupCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeleteEconomicGroupCommand(id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(CrmPermissions.PeopleEdit)
            .WithName("DeleteEconomicGroup")
            .WithSummary("Exclui um grupo econômico sem pessoas vinculadas")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}

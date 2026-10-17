using System.Text.Json.Nodes;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.CustomFields;
using LexFlow.Platform.Domain.CustomFields;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Platform.Endpoints;

public sealed record CreateCustomFieldRequest(
    string Entity,
    string Key,
    CustomFieldType Type,
    string Label,
    string? HelpText,
    bool Required,
    bool Filterable,
    int Position,
    CustomFieldSettings? Settings);

public sealed record UpdateCustomFieldRequest(
    string Label,
    string? HelpText,
    bool Required,
    bool Filterable,
    int Position,
    bool IsActive,
    CustomFieldSettings? Settings);

public sealed record ValidateCustomFieldsRequest(string Entity, JsonObject? Values);

public sealed record LookupTableRequest(string Name);

public sealed record UpdateLookupTableRequest(string Name, bool IsActive);

public sealed record LookupItemRequest(string Name, int? Position);

public sealed record UpdateLookupItemRequest(string Name, int Position, bool IsActive);

/// <summary>
/// Campos customizados e tabelas auxiliares (RF-03) da organização ativa. Ler exige
/// <c>settings.custom_fields.read</c> (todo perfil padrão tem, para montar formulários); criar e editar,
/// <c>settings.custom_fields.manage</c>. Nada é excluído: campos, tabelas e itens são desativados.
/// </summary>
internal static class CustomFieldEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapFields(endpoints);
        MapLookups(endpoints);
    }

    private static void MapFields(IEndpointRouteBuilder endpoints)
    {
        var fields = endpoints.MapGroup("/api/v1/custom-fields").WithTags("CustomFields");

        fields.MapGet("/", async (string? entity, IQueryHandler<ListCustomFieldsQuery, IReadOnlyList<CustomFieldResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListCustomFieldsQuery(entity), cancellationToken)).ToHttpResult())
            .RequirePermission(PlatformPermissions.CustomFieldsRead)
            .WithName("ListCustomFields")
            .WithSummary("Campos customizados de uma entidade (ou de todas), na ordem do formulário")
            .Produces<IReadOnlyList<CustomFieldResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        fields.MapPost("/", async (CreateCustomFieldRequest r, ICommandHandler<CreateCustomFieldCommand, CustomFieldResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateCustomFieldCommand(r.Entity, r.Key, r.Type, r.Label, r.HelpText, r.Required, r.Filterable, r.Position, r.Settings), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created((string?)null, created)))
            .RequirePermission(PlatformPermissions.CustomFieldsManage)
            .WithName("CreateCustomField")
            .WithSummary("Cria um campo customizado; chave, entidade e tipo não mudam depois")
            .Produces<CustomFieldResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        fields.MapPut("/{id:guid}", async (Guid id, UpdateCustomFieldRequest r, ICommandHandler<UpdateCustomFieldCommand, CustomFieldResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdateCustomFieldCommand(id, r.Label, r.HelpText, r.Required, r.Filterable, r.Position, r.IsActive, r.Settings), cancellationToken)).ToHttpResult())
            .RequirePermission(PlatformPermissions.CustomFieldsManage)
            .WithName("UpdateCustomField")
            .WithSummary("Edita rótulo, ajuda, obrigatoriedade, ordem, limites e se o campo está ativo")
            .Produces<CustomFieldResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        fields.MapPost("/validate", async (ValidateCustomFieldsRequest r, IQueryHandler<ValidateCustomFieldValuesQuery, CustomFieldValuesResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ValidateCustomFieldValuesQuery(r.Entity, r.Values), cancellationToken)).ToHttpResult())
            .RequirePermission(PlatformPermissions.CustomFieldsRead)
            .WithName("ValidateCustomFieldValues")
            .WithSummary("Valida valores de campos customizados sem gravar; erros em errors[\"customFields.{chave}\"]")
            .Produces<CustomFieldValuesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static void MapLookups(IEndpointRouteBuilder endpoints)
    {
        var tables = endpoints.MapGroup("/api/v1/lookup-tables").WithTags("LookupTables");

        tables.MapGet("/", async (IQueryHandler<ListLookupTablesQuery, IReadOnlyList<LookupTableResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListLookupTablesQuery(), cancellationToken)).ToHttpResult())
            .RequirePermission(PlatformPermissions.CustomFieldsRead)
            .WithName("ListLookupTables")
            .WithSummary("Tabelas auxiliares com os itens (os inativos vêm marcados)")
            .Produces<IReadOnlyList<LookupTableResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        tables.MapPost("/", async (LookupTableRequest r, ICommandHandler<CreateLookupTableCommand, LookupTableResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateLookupTableCommand(r.Name), cancellationToken)).ToHttpResult(created => TypedResults.Created((string?)null, created)))
            .RequirePermission(PlatformPermissions.CustomFieldsManage)
            .WithName("CreateLookupTable")
            .WithSummary("Cria uma tabela auxiliar do escritório")
            .Produces<LookupTableResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        tables.MapPut("/{id:guid}", async (Guid id, UpdateLookupTableRequest r, ICommandHandler<UpdateLookupTableCommand, LookupTableResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdateLookupTableCommand(id, r.Name, r.IsActive), cancellationToken)).ToHttpResult())
            .RequirePermission(PlatformPermissions.CustomFieldsManage)
            .WithName("UpdateLookupTable")
            .WithSummary("Renomeia ou desativa uma tabela auxiliar (as do sistema ficam sempre ativas)")
            .Produces<LookupTableResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        tables.MapPost("/{id:guid}/items", async (Guid id, LookupItemRequest r, ICommandHandler<CreateLookupItemCommand, LookupItemResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateLookupItemCommand(id, r.Name, r.Position), cancellationToken)).ToHttpResult(created => TypedResults.Created((string?)null, created)))
            .RequirePermission(PlatformPermissions.CustomFieldsManage)
            .WithName("CreateLookupItem")
            .WithSummary("Acrescenta um item à tabela auxiliar (por padrão, no fim)")
            .Produces<LookupItemResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPut("/api/v1/lookup-items/{id:guid}", async (Guid id, UpdateLookupItemRequest r, ICommandHandler<UpdateLookupItemCommand, LookupItemResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdateLookupItemCommand(id, r.Name, r.Position, r.IsActive), cancellationToken)).ToHttpResult())
            .WithTags("LookupTables")
            .RequirePermission(PlatformPermissions.CustomFieldsManage)
            .WithName("UpdateLookupItem")
            .WithSummary("Renomeia, reordena ou desativa um item (itens não são excluídos)")
            .Produces<LookupItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

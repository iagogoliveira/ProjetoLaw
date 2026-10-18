using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Proposals;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Pagination;
using LexFlow.Web.Common.Queries;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace LexFlow.Crm.Endpoints;

public sealed record ProposalNoteRequest(string? Note);

public sealed record ProposalsPageResponse(IReadOnlyList<ProposalSummary> Items, string? NextCursor);

/// <summary>
/// Propostas de honorários (RF-08, Etapa 4, PR 4C): rascunho, aprovação interna, envio com o DOCX congelado, aceite que
/// gera o cliente, e os modelos DOCX do escritório.
/// </summary>
internal static class ProposalEndpoints
{
    /// <summary>Limite do upload de modelo (5 MB), conferido antes de ler o arquivo.</summary>
    private const long MaxTemplateSize = 5 * 1024 * 1024;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        MapQueries(endpoints);
        MapCommands(endpoints);
        MapWorkflow(endpoints);
        MapTemplates(endpoints);
    }

    private static void MapQueries(IEndpointRouteBuilder endpoints)
    {
        var proposals = endpoints.MapGroup("/api/v1/proposals").WithTags("Proposals");

        proposals.MapGet("/", ListAsync)
            .DocumentEnumQuery<ProposalStatus>("status")
            .RequirePermission(CrmPermissions.ProposalsRead)
            .WithName("ListProposals")
            .WithSummary("Propostas de uma oportunidade ou de todas (ex.: aguardando aprovação), das mais recentes às mais antigas")
            .Produces<ProposalsPageResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        proposals.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetProposalQuery, ProposalResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetProposalQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.ProposalsRead)
            .WithName("GetProposal")
            .WithSummary("Uma proposta com os itens e a situação")
            .Produces<ProposalResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        proposals.MapGet("/{id:guid}/document", async (Guid id, IQueryHandler<GetProposalDocumentQuery, ProposalFile> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetProposalDocumentQuery(id), cancellationToken)).ToHttpResult(File))
            .RequirePermission(CrmPermissions.ProposalsRead)
            .WithName("GetProposalDocument")
            .WithSummary("O DOCX da proposta: o congelado no envio, ou gerado na hora enquanto não foi enviada")
            .Produces<Stream>(StatusCodes.Status200OK, ProposalFile.ContentType)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static IResult File(ProposalFile file) => TypedResults.File(file.Content, ProposalFile.ContentType, file.FileName);

    /// <summary>Lista paginada por cursor (<c>?opportunityId=&amp;status=&amp;cursor=&amp;limit=</c>).</summary>
    private static async Task<IResult> ListAsync(Guid? opportunityId, string? status, string? cursor, int? limit, IQueryHandler<ListProposalsQuery, ProposalsPage> handler, CancellationToken cancellationToken)
    {
        var page = CursorPageRequest.Create(cursor, limit);
        if (page.IsFailure)
        {
            return page.Error.ToProblem();
        }

        var statusFilter = QueryEnums.Parse<ProposalStatus>(status);
        if (statusFilter.IsFailure)
        {
            return statusFilter.Error.ToProblem();
        }

        ProposalsPosition? after = null;
        if (page.Value.Cursor is { } value)
        {
            var decoded = CursorCodec.Decode<ProposalsPosition>(value);
            if (decoded.IsFailure)
            {
                return decoded.Error.ToProblem();
            }

            after = decoded.Value;
        }

        var result = await handler.Handle(new ListProposalsQuery(opportunityId, statusFilter.Value, after, page.Value.Limit), cancellationToken);
        return result.ToHttpResult(p => TypedResults.Ok(new ProposalsPageResponse(p.Items, p.Next is null ? null : CursorCodec.Encode(p.Next))));
    }

    private static void MapCommands(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/opportunities/{id:guid}/proposals", async (Guid id, ProposalInput request, ICommandHandler<CreateProposalCommand, ProposalResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateProposalCommand(id, request), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created($"/api/v1/proposals/{created.Id:D}", created)))
            .WithTags("Proposals")
            .RequirePermission(CrmPermissions.ProposalsManage)
            .WithName("CreateProposal")
            .WithSummary("Cria o rascunho de uma proposta para a oportunidade")
            .Produces<ProposalResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var proposals = endpoints.MapGroup("/api/v1/proposals").WithTags("Proposals");

        proposals.MapPut("/{id:guid}", async (Guid id, ProposalInput request, ICommandHandler<UpdateProposalCommand, ProposalResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdateProposalCommand(id, request), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.ProposalsManage)
            .WithName("UpdateProposal")
            .WithSummary("Substitui o rascunho da proposta (título, escopo, condições, validade, modelo e itens)")
            .Produces<ProposalResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        proposals.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeleteProposalCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeleteProposalCommand(id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(CrmPermissions.ProposalsManage)
            .WithName("DeleteProposal")
            .WithSummary("Exclui (logicamente) uma proposta ainda não enviada")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapWorkflow(IEndpointRouteBuilder endpoints)
    {
        var proposals = endpoints.MapGroup("/api/v1/proposals").WithTags("Proposals");

        MapStep(proposals, "submit", ProposalStep.Submit, CrmPermissions.ProposalsManage, "SubmitProposal", "Envia o rascunho para a aprovação interna");
        MapStep(proposals, "approve", ProposalStep.Approve, CrmPermissions.ProposalsApprove, "ApproveProposal", "Aprova a proposta (quem a elaborou não aprova)");
        MapStep(proposals, "return", ProposalStep.ReturnToDraft, CrmPermissions.ProposalsManage, "ReturnProposal", "Devolve a proposta para rascunho, com uma observação (a aprovação sai)");
        MapStep(proposals, "decline", ProposalStep.Decline, CrmPermissions.ProposalsManage, "DeclineProposal", "Registra a recusa do cliente, com uma observação");

        proposals.MapPost("/{id:guid}/send", async (Guid id, ICommandHandler<SendProposalCommand, ProposalResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new SendProposalCommand(id), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.ProposalsManage)
            .WithName("SendProposal")
            .WithSummary("Registra o envio da proposta aprovada, congelando o DOCX gerado")
            .Produces<ProposalResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        proposals.MapPost("/{id:guid}/accept", async (Guid id, ICommandHandler<AcceptProposalCommand, AcceptedProposalResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new AcceptProposalCommand(id), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.ProposalsManage)
            .WithName("AcceptProposal")
            .WithSummary("Registra o aceite: exige o conflito prosseguido, cria o cliente e fecha a oportunidade ganha")
            .Produces<AcceptedProposalResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapStep(RouteGroupBuilder proposals, string action, ProposalStep step, string permission, string name, string summary) =>
        proposals.MapPost($"/{{id:guid}}/{action}", async (Guid id, ProposalNoteRequest? request, ICommandHandler<ChangeProposalCommand, ProposalResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ChangeProposalCommand(id, step, request?.Note), cancellationToken)).ToHttpResult())
            .RequirePermission(permission)
            .WithName(name)
            .WithSummary(summary)
            .Produces<ProposalResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    private static void MapTemplates(IEndpointRouteBuilder endpoints)
    {
        var templates = endpoints.MapGroup("/api/v1/proposal-templates").WithTags("ProposalTemplates");

        templates.MapGet("/", async (IQueryHandler<ListProposalTemplatesQuery, ProposalTemplatesResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListProposalTemplatesQuery(), cancellationToken)).ToHttpResult())
            .RequirePermission(CrmPermissions.ProposalsRead)
            .WithName("ListProposalTemplates")
            .WithSummary("Modelos DOCX do escritório e os marcadores aceitos")
            .Produces<ProposalTemplatesResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        templates.MapGet("/default", async (IQueryHandler<GetDefaultProposalTemplateQuery, ProposalFile> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetDefaultProposalTemplateQuery(), cancellationToken)).ToHttpResult(File))
            .RequirePermission(CrmPermissions.ProposalsRead)
            .WithName("GetDefaultProposalTemplate")
            .WithSummary("O modelo padrão do sistema, para baixar e adaptar")
            .Produces<Stream>(StatusCodes.Status200OK, ProposalFile.ContentType)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        templates.MapPost("/", UploadAsync)
            .DocumentTemplateForm()
            .RequirePermission(CrmPermissions.ProposalsManage)
            .WithName("UploadProposalTemplate")
            .WithSummary("Envia um modelo DOCX (até 5 MB): antivírus, formato e marcadores conferidos antes de guardar")
            .Produces<ProposalTemplateResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        templates.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<DeleteProposalTemplateCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new DeleteProposalTemplateCommand(id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(CrmPermissions.ProposalsManage)
            .WithName("DeleteProposalTemplate")
            .WithSummary("Exclui (logicamente) um modelo; as propostas que o usavam saem no modelo padrão")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Formulário com <c>name</c> e <c>file</c>, lido da requisição: com <c>IFormFile</c> no parâmetro, o roteamento
    /// recusaria outro tipo de conteúdo com 415 antes da autenticação (os SecurityTests exigem 401/403 primeiro). O
    /// tamanho é conferido antes de ler o arquivo.
    /// </summary>
    private static async Task<IResult> UploadAsync(HttpRequest request, ICommandHandler<UploadProposalTemplateCommand, ProposalTemplateResponse> handler, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return CrmErrors.TemplateInvalid.ToProblem();
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files.GetFile("file") is not { } file)
        {
            return CrmErrors.TemplateInvalid.ToProblem();
        }

        if (file.Length > MaxTemplateSize)
        {
            return CrmErrors.TemplateTooLarge.ToProblem();
        }

        var name = form["name"].ToString();

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var result = await handler.Handle(new UploadProposalTemplateCommand(name, file.FileName, buffer.ToArray()), cancellationToken);
        return result.ToHttpResult(created => TypedResults.Created((string?)null, created));
    }
}

/// <summary>Contrato OpenAPI do upload de modelo: <c>multipart/form-data</c> com <c>name</c> e <c>file</c> (binário).</summary>
internal static class TemplateFormDocumentation
{
    public static RouteHandlerBuilder DocumentTemplateForm(this RouteHandlerBuilder builder) =>
        builder.AddOpenApiOperationTransformer((operation, _, _) =>
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
                {
                    ["multipart/form-data"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Required = new HashSet<string>(StringComparer.Ordinal) { "name", "file" },
                            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                            {
                                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                                ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                            },
                        },
                    },
                },
            };
            return Task.CompletedTask;
        });
}

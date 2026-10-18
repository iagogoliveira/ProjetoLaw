using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.CaseImports;
using LexFlow.Litigation.Domain;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.OpenApi;

namespace LexFlow.Litigation.Endpoints;

/// <summary>
/// Importação de processos por planilha (Etapa 5, tarefa 6): envio, pré-visualização, confirmação, relatório das linhas
/// inválidas e o modelo. O processamento é assíncrono: as respostas 202 apontam para <c>GET /api/v1/jobs/{jobId}</c>.
/// </summary>
internal static class CaseImportEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var imports = endpoints.MapGroup("/api/v1/cases").WithTags("CaseImports");

        imports.MapPost("/imports", UploadAsync)
            .DocumentImportForm()
            .RequirePermission(LitigationPermissions.CasesCreate)
            .WithName("StartCaseImport")
            .WithSummary("Envia uma planilha XLSX ou CSV (até 10 MB e 20 mil linhas); a validação roda em segundo plano")
            .Produces<CaseImportStarted>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        imports.MapGet("/imports/template", async (IQueryHandler<GetImportTemplateQuery, ImportFile> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetImportTemplateQuery(), cancellationToken)).ToHttpResult(File))
            .RequirePermission(LitigationPermissions.CasesCreate)
            .WithName("GetCaseImportTemplate")
            .WithSummary("A planilha modelo, com as colunas e uma linha de exemplo")
            .Produces<Stream>(StatusCodes.Status200OK, ImportFile.ContentType)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        imports.MapGet("/imports/{importId:guid}", async (Guid importId, IQueryHandler<GetCaseImportQuery, CaseImportResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetCaseImportQuery(importId), cancellationToken)).ToHttpResult())
            .RequirePermission(LitigationPermissions.CasesCreate)
            .WithName("GetCaseImport")
            .WithSummary("Andamento da importação: totais, pré-visualização e os primeiros erros")
            .Produces<CaseImportResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        imports.MapPost("/imports/{importId:guid}/confirm", async (Guid importId, ICommandHandler<ConfirmCaseImportCommand, CaseImportStarted> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ConfirmCaseImportCommand(importId), cancellationToken)).ToHttpResult(Accepted))
            .RequirePermission(LitigationPermissions.CasesCreate)
            .WithName("ConfirmCaseImport")
            .WithSummary("Confirma a pré-visualização: grava as linhas válidas em segundo plano")
            .Produces<CaseImportStarted>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        imports.MapGet("/imports/{importId:guid}/errors", async (Guid importId, IQueryHandler<GetCaseImportReportQuery, ImportFile> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetCaseImportReportQuery(importId), cancellationToken)).ToHttpResult(File))
            .RequirePermission(LitigationPermissions.CasesCreate)
            .WithName("GetCaseImportErrors")
            .WithSummary("Relatório das linhas inválidas (XLSX: linha, coluna, valor, código e mensagem)")
            .Produces<Stream>(StatusCodes.Status200OK, ImportFile.ContentType)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static IResult File(ImportFile file) => TypedResults.File(file.Content, ImportFile.ContentType, file.FileName);

    private static IResult Accepted(CaseImportStarted started) => TypedResults.Accepted($"/api/v1/jobs/{started.JobId:D}", started);

    /// <summary>
    /// O arquivo vem no campo <c>file</c> do formulário, lido da requisição (com <c>IFormFile</c> no parâmetro, o
    /// roteamento responderia 415 antes da autenticação). O tamanho é conferido antes de ler.
    /// </summary>
    private static async Task<IResult> UploadAsync(HttpRequest request, ICommandHandler<StartCaseImportCommand, CaseImportStarted> handler, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return LitigationErrors.ImportFileInvalid.ToProblem();
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (form.Files.GetFile("file") is not { } file)
        {
            return LitigationErrors.ImportFileInvalid.ToProblem();
        }

        if (file.Length > ImportColumns.MaxFileSize)
        {
            return LitigationErrors.ImportFileTooLarge.ToProblem();
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var result = await handler.Handle(new StartCaseImportCommand(file.FileName, buffer.ToArray()), cancellationToken);
        return result.ToHttpResult(Accepted);
    }

    private static RouteHandlerBuilder DocumentImportForm(this RouteHandlerBuilder builder) =>
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
                            Required = new HashSet<string>(StringComparer.Ordinal) { "file" },
                            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
                            {
                                ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                            },
                        },
                    },
                },
            };
            return Task.CompletedTask;
        });
}

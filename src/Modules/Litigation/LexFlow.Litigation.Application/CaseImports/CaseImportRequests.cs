using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.CaseImports;
using LexFlow.Platform.Contracts.Jobs;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.CaseImports;

/// <summary>A importação recebida e o job que a processa (<c>GET /api/v1/jobs/{jobId}</c>).</summary>
public sealed record CaseImportStarted(Guid ImportId, Guid JobId);

public sealed record CaseImportErrorResponse(int Row, string Column, string? Value, string Code, string Message);

/// <summary>
/// Estado da importação: os totais, a pré-visualização (as primeiras linhas válidas) e os primeiros erros; o relatório
/// completo das linhas inválidas é baixado à parte.
/// </summary>
public sealed record CaseImportResponse(
    Guid Id,
    string FileName,
    CaseImportStatus Status,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int ImportedRows,
    IReadOnlyList<ImportPreviewRow> Preview,
    IReadOnlyList<CaseImportErrorResponse> Errors,
    string? ErrorCode,
    Guid ValidationJobId,
    Guid? ImportJobId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Arquivo para baixar (modelo ou relatório).</summary>
public sealed record ImportFile(byte[] Content, string FileName)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

/// <summary>Recebe a planilha (até 10 MB e 20 mil linhas), passa no antivírus, guarda e agenda a validação.</summary>
public sealed record StartCaseImportCommand(string FileName, byte[] Content) : ICommand<CaseImportStarted>;

public sealed record GetCaseImportQuery(Guid Id) : IQuery<CaseImportResponse>;

/// <summary>Confirma a pré-visualização: agenda a gravação das linhas válidas.</summary>
public sealed record ConfirmCaseImportCommand(Guid Id) : ICommand<CaseImportStarted>;

/// <summary>O relatório das linhas inválidas (XLSX: linha, coluna, valor, código e mensagem).</summary>
public sealed record GetCaseImportReportQuery(Guid Id) : IQuery<ImportFile>;

public sealed record GetImportTemplateQuery : IQuery<ImportFile>;

internal sealed class StartCaseImportHandler(
    ILitigationDbContext db,
    ICaseSheets sheets,
    IFileScanner scanner,
    IObjectStorage storage,
    IJobTracker jobs,
    IJobScheduler scheduler,
    ITenantContext tenant,
    IIdGenerator ids) : ICommandHandler<StartCaseImportCommand, CaseImportStarted>
{
    public async Task<Result<CaseImportStarted>> Handle(StartCaseImportCommand command, CancellationToken cancellationToken)
    {
        if (command.Content.Length == 0)
        {
            return LitigationErrors.ImportFileInvalid;
        }

        if (command.Content.Length > ImportColumns.MaxFileSize)
        {
            return LitigationErrors.ImportFileTooLarge;
        }

        using (var scanned = new MemoryStream(command.Content, writable: false))
        {
            if ((await scanner.ScanAsync(scanned, cancellationToken)).Status != FileScanStatus.Clean)
            {
                return LitigationErrors.ImportFileInfected;
            }
        }

        // Formato, cabeçalho e tamanho são conferidos já no envio; as linhas, no job.
        using (var read = new MemoryStream(command.Content, writable: false))
        {
            var sheet = sheets.Read(read, command.FileName);
            if (sheet.IsFailure)
            {
                return sheet.Error!;
            }
        }

        var importId = ids.NewId();
        using (var content = new MemoryStream(command.Content, writable: false))
        {
            await storage.PutAsync(CaseImportJobs.FileKey(tenant.RequiredOrganizationId, importId), content, "application/octet-stream", cancellationToken);
        }

        var jobId = await jobs.CreateAsync("cases.import.validate", cancellationToken);
        db.CaseImports.Add(CaseImport.Start(importId, command.FileName, jobId));
        await db.SaveChangesAsync(cancellationToken);
        scheduler.Enqueue<CaseImportJobs>(job => job.ValidateAsync(importId, jobId));
        return new CaseImportStarted(importId, jobId);
    }
}

internal sealed class GetCaseImportHandler(ILitigationDbContext db) : IQueryHandler<GetCaseImportQuery, CaseImportResponse>
{
    public const int ErrorsShown = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<CaseImportResponse>> Handle(GetCaseImportQuery query, CancellationToken cancellationToken)
    {
        var import = await db.CaseImports.AsNoTracking().SingleOrDefaultAsync(i => i.Id == query.Id, cancellationToken);
        if (import is null)
        {
            return LitigationErrors.ImportNotFound;
        }

        var errors = await db.ImportErrors.AsNoTracking().Where(e => e.ImportId == import.Id)
            .OrderBy(e => e.Row).ThenBy(e => e.Column).Take(ErrorsShown)
            .Select(e => new CaseImportErrorResponse(e.Row, e.Column, e.Value, e.Code, e.Message))
            .ToListAsync(cancellationToken);
        return new CaseImportResponse(
            import.Id,
            import.FileName,
            import.Status,
            import.TotalRows,
            import.ValidRows,
            import.InvalidRows,
            import.ImportedRows,
            JsonSerializer.Deserialize<List<ImportPreviewRow>>(import.Preview, Json) ?? [],
            errors,
            import.ErrorCode,
            import.ValidationJobId,
            import.ImportJobId,
            import.CreatedAt,
            import.CompletedAt);
    }
}

internal sealed class ConfirmCaseImportHandler(ILitigationDbContext db, IJobTracker jobs, IJobScheduler scheduler) : ICommandHandler<ConfirmCaseImportCommand, CaseImportStarted>
{
    public async Task<Result<CaseImportStarted>> Handle(ConfirmCaseImportCommand command, CancellationToken cancellationToken)
    {
        var import = await db.CaseImports.SingleOrDefaultAsync(i => i.Id == command.Id, cancellationToken);
        if (import is null)
        {
            return LitigationErrors.ImportNotFound;
        }

        if (import.Status != CaseImportStatus.Ready || import.ValidRows == 0)
        {
            return import.Status != CaseImportStatus.Ready ? LitigationErrors.ImportNotReady : LitigationErrors.ImportNothingValid;
        }

        var jobId = await jobs.CreateAsync("cases.import.run", cancellationToken);
        import.Confirm(jobId);
        await db.SaveChangesAsync(cancellationToken);
        var importId = import.Id;
        scheduler.Enqueue<CaseImportJobs>(job => job.ImportAsync(importId, jobId));
        return new CaseImportStarted(importId, jobId);
    }
}

internal sealed class GetCaseImportReportHandler(ILitigationDbContext db, ICaseSheets sheets) : IQueryHandler<GetCaseImportReportQuery, ImportFile>
{
    public async Task<Result<ImportFile>> Handle(GetCaseImportReportQuery query, CancellationToken cancellationToken)
    {
        var import = await db.CaseImports.AsNoTracking().SingleOrDefaultAsync(i => i.Id == query.Id, cancellationToken);
        if (import is null)
        {
            return LitigationErrors.ImportNotFound;
        }

        var errors = await db.ImportErrors.AsNoTracking().Where(e => e.ImportId == import.Id)
            .OrderBy(e => e.Row).ThenBy(e => e.Column)
            .ToListAsync(cancellationToken);
        var name = Path.GetFileNameWithoutExtension(import.FileName);
        return new ImportFile(sheets.ErrorReport(errors), $"{name}-erros.xlsx");
    }
}

internal sealed class GetImportTemplateHandler(ICaseSheets sheets) : IQueryHandler<GetImportTemplateQuery, ImportFile>
{
    public Task<Result<ImportFile>> Handle(GetImportTemplateQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Ok(new ImportFile(sheets.Template(), "modelo-importacao-processos.xlsx")));
}

using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Domain.CaseImports;

/// <summary>Andamento da importação de processos.</summary>
public enum CaseImportStatus
{
    /// <summary>O arquivo foi recebido e está sendo validado.</summary>
    Validating,

    /// <summary>Validado: a pré-visualização está pronta e espera a confirmação.</summary>
    Ready,

    /// <summary>Confirmado: as linhas válidas estão sendo gravadas.</summary>
    Importing,

    Completed,

    /// <summary>O arquivo não pôde ser lido (formato, cabeçalho, tamanho) ou o processamento falhou.</summary>
    Failed,
}

/// <summary>
/// Importação de processos por planilha (Etapa 5, tarefa 6; decisões de 2026-10-18): o arquivo é validado linha a
/// linha num job, a pré-visualização mostra os totais e os erros, e, confirmada, as linhas válidas são gravadas em
/// lotes; as inválidas ficam no relatório. A gravação anota a última linha feita para retomar sem duplicar.
/// </summary>
public sealed class CaseImport : AggregateRoot<Guid>, ITenantOwned, IAuditable
{
    public const int MaxFileNameLength = 255;

    private CaseImport()
    {
    }

    private CaseImport(Guid id, string fileName, Guid jobId)
        : base(id)
    {
        FileName = fileName;
        Status = CaseImportStatus.Validating;
        ValidationJobId = jobId;
    }

    public Guid OrganizationId { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public CaseImportStatus Status { get; private set; }

    public int TotalRows { get; private set; }

    public int InvalidRows { get; private set; }

    public int ImportedRows { get; private set; }

    /// <summary>Última linha da planilha já gravada (para retomar depois de uma falha).</summary>
    public int LastImportedRow { get; private set; }

    public Guid ValidationJobId { get; private set; }

    public Guid? ImportJobId { get; private set; }

    public string? ErrorCode { get; private set; }

    /// <summary>As primeiras linhas válidas, em JSON, para a pré-visualização.</summary>
    public string Preview { get; private set; } = "[]";

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public int ValidRows => TotalRows - InvalidRows;

    public static CaseImport Start(Guid id, string fileName, Guid validationJobId)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        return new CaseImport(id, name.Length == 0 ? "planilha" : name[..Math.Min(name.Length, MaxFileNameLength)], validationJobId);
    }

    public void Validated(int totalRows, int invalidRows, string preview)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preview);
        TotalRows = totalRows;
        InvalidRows = invalidRows;
        Preview = preview;
        Status = CaseImportStatus.Ready;
    }

    /// <summary>Confirmação da pré-visualização: só uma vez, e com alguma linha válida.</summary>
    public Result Confirm(Guid importJobId)
    {
        if (Status != CaseImportStatus.Ready)
        {
            return LitigationErrors.ImportNotReady;
        }

        if (ValidRows == 0)
        {
            return LitigationErrors.ImportNothingValid;
        }

        Status = CaseImportStatus.Importing;
        ImportJobId = importJobId;
        return Result.Success;
    }

    /// <summary>Revalidação na hora de gravar (os cadastros podem ter mudado desde a pré-visualização).</summary>
    public void Revalidated(int totalRows, int invalidRows)
    {
        TotalRows = totalRows;
        InvalidRows = invalidRows;
    }

    public void Progress(int lastRow, int imported)
    {
        LastImportedRow = lastRow;
        ImportedRows += imported;
    }

    public void Complete(DateTimeOffset now)
    {
        Status = CaseImportStatus.Completed;
        CompletedAt = now;
    }

    public void Fail(string errorCode, DateTimeOffset now)
    {
        Status = CaseImportStatus.Failed;
        ErrorCode = errorCode;
        CompletedAt = now;
    }
}

/// <summary>Um erro de uma linha da planilha: a linha, a coluna, o valor lido, o código estável e a mensagem.</summary>
public sealed class CaseImportError : ITenantOwned
{
    public const int MaxValueLength = 200;

    private CaseImportError()
    {
    }

    public CaseImportError(Guid importId, int row, string column, string? value, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Id = Guid.CreateVersion7();
        ImportId = importId;
        Row = row;
        Column = column;
        Value = value is null ? null : value[..Math.Min(value.Length, MaxValueLength)];
        Code = error.Code;
        Message = error.Message;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid ImportId { get; private set; }

    /// <summary>Número da linha na planilha (o cabeçalho é a linha 1).</summary>
    public int Row { get; private set; }

    public string Column { get; private set; } = string.Empty;

    public string? Value { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;
}

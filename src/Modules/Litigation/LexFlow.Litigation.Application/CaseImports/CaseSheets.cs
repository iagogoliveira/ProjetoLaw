using LexFlow.Litigation.Domain.CaseImports;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Application.CaseImports;

/// <summary>Uma linha lida da planilha: o número (o cabeçalho é a linha 1) e os valores por coluna (nomes do modelo).</summary>
public sealed record SheetRow(int Number, IReadOnlyDictionary<string, string?> Values)
{
    public string? this[string column] => Values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

public sealed record SheetData(IReadOnlyList<SheetRow> Rows);

/// <summary>
/// Planilhas da importação (implementado na Infrastructure com o ClosedXML): leitura de XLSX e CSV para linhas com os
/// nomes de coluna do modelo, o modelo para baixar e o relatório das linhas inválidas.
/// </summary>
public interface ICaseSheets
{
    /// <summary>Lê o arquivo; formato, cabeçalho e tamanho errados são erros do arquivo inteiro.</summary>
    Result<SheetData> Read(Stream content, string fileName);

    byte[] Template();

    byte[] ErrorReport(IReadOnlyList<CaseImportError> errors);
}

/// <summary>As colunas do modelo de importação (cabeçalho da linha 1, sem diferença de maiúsculas e acentos).</summary>
public static class ImportColumns
{
    public const string Kind = "tipo";
    public const string Cnj = "cnj";
    public const string Folder = "pasta";
    public const string Instance = "instancia";
    public const string District = "comarca";
    public const string CourtUnit = "vara";
    public const string Rite = "rito";
    public const string ProceduralClass = "classe";
    public const string Subject = "assunto";
    public const string ActionType = "tipo_acao";
    public const string PracticeArea = "area";
    public const string Branch = "filial";
    public const string Phase = "fase";
    public const string ClaimValue = "valor_causa";
    public const string FiledOn = "distribuicao";
    public const string ServedOn = "citacao";
    public const string ClientName = "cliente_nome";
    public const string ClientDocument = "cliente_documento";
    public const string ClientPole = "cliente_polo";
    public const string OpponentName = "contraria_nome";
    public const string OpponentDocument = "contraria_documento";
    public const string LawyerEmail = "advogado_email";
    public const string Description = "descricao";

    public const int MaxRows = 20_000;
    public const long MaxFileSize = 10 * 1024 * 1024;

    public static IReadOnlyList<string> All { get; } =
    [
        Kind, Cnj, Folder, Instance, District, CourtUnit, Rite, ProceduralClass, Subject, ActionType, PracticeArea, Branch, Phase,
        ClaimValue, FiledOn, ServedOn, ClientName, ClientDocument, ClientPole, OpponentName, OpponentDocument, LawyerEmail, Description,
    ];

    public static IReadOnlyList<string> Required { get; } = [Kind, ClientName];
}

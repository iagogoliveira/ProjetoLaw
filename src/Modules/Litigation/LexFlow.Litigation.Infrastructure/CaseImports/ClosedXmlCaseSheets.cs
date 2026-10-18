using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using LexFlow.Litigation.Application.CaseImports;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.CaseImports;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Infrastructure.CaseImports;

/// <summary>
/// Planilhas da importação com o ClosedXML (ADR 0029): lê a primeira aba do XLSX ou um CSV (separador ";" ou ",", em
/// UTF-8, com aspas), pela linha 1 como cabeçalho; gera o modelo e o relatório das linhas inválidas. Datas e números
/// das células saem no formato que a validação espera (<c>yyyy-MM-dd</c> e ponto decimal).
/// </summary>
internal sealed class ClosedXmlCaseSheets : ICaseSheets
{
    public Result<SheetData> Read(Stream content, string fileName)
    {
        ArgumentNullException.ThrowIfNull(content);
        var extension = Path.GetExtension(fileName ?? string.Empty).ToUpperInvariant();
        var table = extension switch
        {
            ".XLSX" => ReadXlsx(content),
            ".CSV" => ReadCsv(content),
            _ => null,
        };
        if (table is null || table.Count == 0)
        {
            return LitigationErrors.ImportFileInvalid;
        }

        var headers = table[0].Select(CaseRowReaderKey).ToList();
        if (ImportColumns.Required.Any(required => !headers.Contains(required, StringComparer.Ordinal)))
        {
            return LitigationErrors.ImportHeaderMissing;
        }

        var rows = new List<SheetRow>();
        for (var i = 1; i < table.Count; i++)
        {
            var cells = table[i];
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var c = 0; c < headers.Count; c++)
            {
                if (headers[c].Length > 0)
                {
                    values[headers[c]] = c < cells.Count ? cells[c] : null;
                }
            }

            rows.Add(new SheetRow(i + 1, values));
            if (rows.Count > ImportColumns.MaxRows)
            {
                return LitigationErrors.ImportTooManyRows;
            }
        }

        return new SheetData(rows);
    }

    public byte[] Template()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Processos");
        for (var c = 0; c < ImportColumns.All.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = ImportColumns.All[c];
        }

        string[] example =
        [
            "judicial", "2238058-98.2025.8.26.8434", "", "1", "São Paulo", "1ª Vara Cível", "Procedimento Comum", "", "", "", "", "", "", "150000,00",
            "10/01/2025", "", "Banco Alfa S.A.", "11.444.777/0001-61", "passivo", "José da Silva", "", "", "Cobrança indevida",
        ];
        for (var c = 0; c < example.Length; c++)
        {
            sheet.Cell(2, c + 1).Value = example[c];
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();
        return Save(workbook);
    }

    public byte[] ErrorReport(IReadOnlyList<CaseImportError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Linhas inválidas");
        string[] headers = ["linha", "coluna", "valor", "codigo", "mensagem"];
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        var r = 2;
        foreach (var error in errors)
        {
            sheet.Cell(r, 1).Value = error.Row;
            sheet.Cell(r, 2).Value = error.Column;
            sheet.Cell(r, 3).Value = error.Value ?? string.Empty;
            sheet.Cell(r, 4).Value = error.Code;
            sheet.Cell(r, 5).Value = error.Message;
            r++;
        }

        sheet.Row(1).Style.Font.Bold = true;
        return Save(workbook);
    }

    /// <summary>Nome da coluna como no modelo: sem acentos, minúsculo e com "_" no lugar dos espaços.</summary>
    private static string CaseRowReaderKey(string? header) => CaseRowReader.Key(header).Replace(' ', '_');

    private static byte[] Save(XLWorkbook workbook)
    {
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static List<List<string?>>? ReadXlsx(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet?.LastRowUsed() is not { } lastRow || sheet.LastColumnUsed() is not { } lastColumn)
            {
                return [];
            }

            var table = new List<List<string?>>();
            for (var r = 1; r <= lastRow.RowNumber(); r++)
            {
                var row = new List<string?>();
                for (var c = 1; c <= lastColumn.ColumnNumber(); c++)
                {
                    row.Add(Text(sheet.Cell(r, c)));
                }

                table.Add(row);
            }

            return table;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or FormatException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    private static string? Text(IXLCell cell)
    {
        var value = cell.Value;
        return value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Number => value.GetNumber().ToString("0.############", CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "true" : "false",
            _ => cell.GetFormattedString(),
        };
    }

    /// <summary>CSV com o separador da primeira linha (";" no Excel em português, "," nos demais), aspas duplas e quebras dentro de aspas.</summary>
    private static List<List<string?>>? ReadCsv(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        if (text.Length == 0)
        {
            return [];
        }

        var end = text.IndexOfAny(['\r', '\n']);
        var firstLine = end >= 0 ? text[..end] : text;
        var separator = firstLine.Count(c => c == ';') >= firstLine.Count(c => c == ',') ? ';' : ',';
        return SplitCsv(text, separator);
    }

    private static List<List<string?>>? SplitCsv(string text, char separator)
    {
        var table = new List<List<string?>>();
        var row = new List<string?>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                // Dentro de aspas, "" é uma aspa e " fecha o campo.
                var escaped = c == '"' && i + 1 < text.Length && text[i + 1] == '"';
                quoted = c != '"' || escaped;
                i += escaped ? 1 : 0;
                cell.Append(c == '"' && !escaped ? string.Empty : c.ToString());
                continue;
            }

            if (c == '"')
            {
                quoted = true;
            }
            else if (c == separator)
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                cell.Clear();
                table.Add(row);
                row = [];
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            table.Add(row);
        }

        return quoted ? null : table;
    }
}

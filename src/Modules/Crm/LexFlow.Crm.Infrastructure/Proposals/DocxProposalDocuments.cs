using System.Globalization;
using System.IO.Packaging;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Crm.Infrastructure.Proposals;

/// <summary>
/// Mesclagem simples de modelos DOCX (ADR 0024) sobre o Open XML SDK. Marcadores <c>{{grupo.campo}}</c> no corpo,
/// cabeçalhos e rodapés; a linha de tabela que tiver um marcador <c>{{item.*}}</c> é repetida para cada item. O Word
/// costuma partir o texto em vários trechos (<c>w:r</c>): o marcador inteiro num trecho é trocado ali mesmo, mantendo a
/// formatação; o partido entre trechos junta o parágrafo no primeiro trecho (e a formatação dele vale para o parágrafo).
/// </summary>
internal sealed partial class DocxProposalDocuments : IProposalDocuments
{
    private const string ItemPrefix = "item.";

    private static readonly NumberFormatInfo Brazilian = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
    };

    private static readonly Lazy<byte[]> Default = new(BuildDefaultTemplate);

    /// <summary>Larguras das colunas da tabela de honorários do modelo padrão (em vigésimos de ponto).</summary>
    private static readonly int[] ColumnWidths = [3400, 1400, 1300, 1500, 1500];

    public IReadOnlyList<string> Markers { get; } =
    [
        "cliente.nome", "cliente.documento", "cliente.email",
        "oportunidade.titulo",
        "proposta.titulo", "proposta.escopo", "proposta.condicoes", "proposta.data", "proposta.validade", "proposta.total",
        "item.descricao", "item.modalidade", "item.quantidade", "item.valor", "item.total",
    ];

    [GeneratedRegex(@"\{\{\s*(?<name>[a-z_]+\.[a-z_]+)\s*\}\}", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Marker();

    public byte[] DefaultTemplate() => Default.Value;

    public Result Validate(Stream docx)
    {
        ArgumentNullException.ThrowIfNull(docx);
        try
        {
            using var copy = Copy(docx);
            using var document = WordprocessingDocument.Open(copy, false);
            if (document.DocumentType != WordprocessingDocumentType.Document || document.MainDocumentPart?.Document?.Body is null)
            {
                return CrmErrors.TemplateInvalid;
            }

            var unknown = Roots(document)
                .SelectMany(root => root.Descendants<Paragraph>())
                .SelectMany(p => Marker().Matches(TextOf(p)).Select(m => m.Groups["name"].Value.ToLowerInvariant()))
                .Where(marker => !Markers.Contains(marker, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            return unknown.Count == 0 ? Result.Success : CrmErrors.TemplateUnknownMarkers(unknown);
        }
        catch (Exception exception) when (exception is OpenXmlPackageException or InvalidDataException or FileFormatException or System.Xml.XmlException)
        {
            return CrmErrors.TemplateInvalid;
        }
    }

    public byte[] Render(Stream? model, ProposalDocumentData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var output = model is null ? Copy(new MemoryStream(Default.Value, writable: false)) : Copy(model);
        using (var document = WordprocessingDocument.Open(output, true))
        {
            var values = ValuesOf(data);
            foreach (var root in Roots(document))
            {
                RepeatItemRows(root, data.Items);
                foreach (var paragraph in root.Descendants<Paragraph>().ToList())
                {
                    Replace(paragraph, values);
                }
            }

            document.MainDocumentPart!.Document!.Save();
        }

        return output.ToArray();
    }

    /// <summary>Corpo, cabeçalhos e rodapés.</summary>
    private static IEnumerable<OpenXmlElement> Roots(WordprocessingDocument document)
    {
        var main = document.MainDocumentPart!;
        if (main.Document?.Body is { } body)
        {
            yield return body;
        }

        foreach (var header in main.HeaderParts.Select(h => h.Header).OfType<Header>())
        {
            yield return header;
        }

        foreach (var footer in main.FooterParts.Select(f => f.Footer).OfType<Footer>())
        {
            yield return footer;
        }
    }

    private static string TextOf(OpenXmlElement element) => string.Concat(element.Descendants<Text>().Select(t => t.Text));

    /// <summary>Linhas com <c>{{item.*}}</c>: uma cópia por item, com os valores dele, no lugar da linha-modelo.</summary>
    private static void RepeatItemRows(OpenXmlElement root, IReadOnlyList<ProposalDocumentItem> items)
    {
        var templateRows = root.Descendants<TableRow>()
            .Where(row => Marker().Matches(TextOf(row)).Any(m => m.Groups["name"].Value.StartsWith(ItemPrefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        foreach (var row in templateRows)
        {
            foreach (var item in items)
            {
                var copy = (TableRow)row.CloneNode(true);
                var values = ItemValuesOf(item);
                foreach (var paragraph in copy.Descendants<Paragraph>().ToList())
                {
                    Replace(paragraph, values);
                }

                row.InsertBeforeSelf(copy);
            }

            row.Remove();
        }
    }

    /// <summary>Troca os marcadores de um parágrafo (os desconhecidos ficam vazios).</summary>
    private static void Replace(Paragraph paragraph, Dictionary<string, string> values)
    {
        var texts = paragraph.Descendants<Text>().ToList();
        var whole = string.Concat(texts.Select(t => t.Text));
        var matches = Marker().Matches(whole);
        if (matches.Count == 0)
        {
            return;
        }

        string Evaluate(Match match) => values.TryGetValue(match.Groups["name"].Value.ToLowerInvariant(), out var value) ? value : string.Empty;

        // Cada marcador inteiro dentro de um trecho: troca no próprio trecho, sem mexer na formatação.
        if (texts.Sum(t => Marker().Count(t.Text)) == matches.Count)
        {
            foreach (var text in texts.Where(t => Marker().IsMatch(t.Text)))
            {
                SetText(text, Marker().Replace(text.Text, Evaluate));
            }

            return;
        }

        // Marcador partido entre trechos: o parágrafo inteiro vai para o primeiro trecho.
        SetText(texts[0], Marker().Replace(whole, Evaluate));
        foreach (var text in texts.Skip(1))
        {
            text.Remove();
        }
    }

    /// <summary>Texto com quebras de linha: um <c>w:t</c> por linha, separados por <c>w:br</c>.</summary>
    private static void SetText(Text text, string value)
    {
        var lines = value.ReplaceLineEndings("\n").Split('\n');
        text.Text = lines[0];
        text.Space = SpaceProcessingModeValues.Preserve;
        OpenXmlElement last = text;
        foreach (var line in lines.Skip(1))
        {
            var lineBreak = new Break();
            last.InsertAfterSelf(lineBreak);
            var next = new Text(line) { Space = SpaceProcessingModeValues.Preserve };
            lineBreak.InsertAfterSelf(next);
            last = next;
        }
    }

    private static Dictionary<string, string> ValuesOf(ProposalDocumentData data) => new(StringComparer.Ordinal)
    {
        ["cliente.nome"] = data.ClientName,
        ["cliente.documento"] = FormatDocument(data.ClientDocument),
        ["cliente.email"] = data.ClientEmail ?? string.Empty,
        ["oportunidade.titulo"] = data.OpportunityTitle,
        ["proposta.titulo"] = data.Title,
        ["proposta.escopo"] = data.Scope ?? string.Empty,
        ["proposta.condicoes"] = data.Conditions ?? string.Empty,
        ["proposta.data"] = FormatDate(data.Date),
        ["proposta.validade"] = data.ValidUntil is { } validUntil ? FormatDate(validUntil) : string.Empty,
        ["proposta.total"] = FormatMoney(data.Total),
    };

    private static Dictionary<string, string> ItemValuesOf(ProposalDocumentItem item) => new(StringComparer.Ordinal)
    {
        ["item.descricao"] = item.Description,
        ["item.modalidade"] = KindName(item.Kind),
        ["item.quantidade"] = item.Kind == FeeKind.Success ? string.Empty : item.Quantity.ToString("#,0.##", Brazilian),
        ["item.valor"] = item.Kind == FeeKind.Success
            ? $"{item.Percentage!.Value.ToString("0.##", Brazilian)}% do benefício"
            : FormatMoney(item.UnitAmount ?? 0m),
        ["item.total"] = item.Amount is { } amount ? FormatMoney(amount) : "conforme o êxito",
    };

    public static string KindName(FeeKind kind) => kind switch
    {
        FeeKind.Fixed => "Fixo",
        FeeKind.Monthly => "Mensal",
        FeeKind.Hourly => "Por hora",
        FeeKind.Success => "Êxito",
        _ => "Por ato",
    };

    public static string FormatMoney(decimal value) => $"R$ {value.ToString("#,0.00", Brazilian)}";

    private static string FormatDate(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string FormatDocument(string? document) => document switch
    {
        null => string.Empty,
        { Length: 11 } when Cpf.Create(document) is { IsSuccess: true } cpf => cpf.Value.Formatted,
        { Length: 14 } when Cnpj.Create(document) is { IsSuccess: true } cnpj => cnpj.Value.Formatted,
        _ => document,
    };

    /// <summary>Cópia expansível (o SDK grava no próprio fluxo).</summary>
    private static MemoryStream Copy(Stream source)
    {
        var copy = new MemoryStream();
        source.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    /// <summary>
    /// O modelo padrão do sistema, montado em código (sem binário no repositório): título, dados do cliente, escopo,
    /// tabela de honorários com a linha de itens e condições.
    /// </summary>
    private static byte[] BuildDefaultTemplate()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body(
                Paragraph("Proposta de honorários", bold: true, size: 32),
                Paragraph("{{proposta.titulo}}", bold: true, size: 26),
                Paragraph("Cliente: {{cliente.nome}}"),
                Paragraph("CPF/CNPJ: {{cliente.documento}}"),
                Paragraph("Data: {{proposta.data}}"),
                Paragraph("Válida até: {{proposta.validade}}"),
                Paragraph("Escopo", bold: true, size: 24),
                Paragraph("{{proposta.escopo}}"),
                Paragraph("Honorários", bold: true, size: 24),
                FeeTable(),
                Paragraph("Total (sem os honorários de êxito): {{proposta.total}}", bold: true),
                Paragraph("Condições", bold: true, size: 24),
                Paragraph("{{proposta.condicoes}}"),
                new SectionProperties(new PageMargin { Top = 1418, Bottom = 1418, Left = 1418, Right = 1418 }));
            main.Document = new Document(body);
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static Table FeeTable()
    {
        // A ordem dos elementos segue o esquema do Word (conferida pelo OpenXmlValidator nos testes).
        var border = new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 });
        return new Table(
            new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, border),
            new TableGrid([.. ColumnWidths.Select(width => new GridColumn { Width = width.ToString(CultureInfo.InvariantCulture) })]),
            Row(true, "Descrição", "Modalidade", "Quantidade", "Valor", "Total"),
            Row(false, "{{item.descricao}}", "{{item.modalidade}}", "{{item.quantidade}}", "{{item.valor}}", "{{item.total}}"));
    }

    private static TableRow Row(bool header, params string[] cells) =>
        new([.. cells.Select(text => new TableCell(Paragraph(text, bold: header)))]);

    private static Paragraph Paragraph(string text, bool bold = false, int? size = null)
    {
        var properties = new RunProperties();
        if (bold)
        {
            properties.Append(new Bold());
        }

        if (size is { } halfPoints)
        {
            properties.Append(new FontSize { Val = halfPoints.ToString(CultureInfo.InvariantCulture) });
        }

        return new Paragraph(new Run(properties, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }
}

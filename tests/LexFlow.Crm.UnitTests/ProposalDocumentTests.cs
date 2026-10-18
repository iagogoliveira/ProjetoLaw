using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.Crm.Infrastructure.Proposals;

namespace LexFlow.Crm.UnitTests;

public sealed class ProposalDocumentTests
{
    private readonly DocxProposalDocuments _documents = new();

    private static ProposalDocumentData Data(string? scope = "Defesa nas reclamações trabalhistas") => new(
        "Construtora Horizonte S.A.",
        "11222333000181",
        "contato@horizonte.com.br",
        "Contencioso trabalhista",
        "Proposta 2026",
        scope,
        "Pagamento até o dia 10.",
        new DateOnly(2026, 10, 18),
        new DateOnly(2026, 11, 17),
        43_234.5m,
        [
            new ProposalDocumentItem("Acompanhamento mensal", FeeKind.Monthly, 12, 3_500m, null, 42_000m),
            new ProposalDocumentItem("Audiências", FeeKind.PerAct, 1.5m, 823m, null, 1_234.5m),
            new ProposalDocumentItem("Êxito", FeeKind.Success, 1, null, 20m, null),
        ]);

    /// <summary>DOCX mínimo com um parágrafo por lista de trechos (cada trecho vira um <c>w:r</c>).</summary>
    private static MemoryStream Docx(params string[][] paragraphs)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body([.. paragraphs.Select(runs => new Paragraph([.. runs.Select(text => new Run(new Text(text)))]))]));
            main.Document.Save();
        }

        stream.Position = 0;
        return stream;
    }

    private static (string Text, List<List<string>> Rows, int Breaks) Read(byte[] docx)
    {
        using var stream = new MemoryStream(docx);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart!.Document!.Body!;
        var rows = body.Descendants<TableRow>().Select(r => r.Descendants<TableCell>().Select(c => c.InnerText).ToList()).ToList();
        return (string.Join('\n', body.Descendants<Paragraph>().Select(p => p.InnerText)), rows, body.Descendants<Break>().Count());
    }

    [Fact]
    public void The_default_template_fills_the_markers_and_repeats_the_item_row()
    {
        var (text, rows, _) = Read(_documents.Render(null, Data()));

        text.ShouldContain("Cliente: Construtora Horizonte S.A.");
        text.ShouldContain("CPF/CNPJ: 11.222.333/0001-81");
        text.ShouldContain("Data: 18/10/2026");
        text.ShouldContain("Válida até: 17/11/2026");
        text.ShouldContain("R$ 43.234,50");
        text.ShouldNotContain("{{");
        rows.Count.ShouldBe(4);
        rows[1].ShouldBe(["Acompanhamento mensal", "Mensal", "12", "R$ 3.500,00", "R$ 42.000,00"]);
        rows[2].ShouldBe(["Audiências", "Por ato", "1,5", "R$ 823,00", "R$ 1.234,50"]);
        rows[3].ShouldBe(["Êxito", "Êxito", string.Empty, "20% do benefício", "conforme o êxito"]);
    }

    [Fact]
    public void The_generated_document_is_valid_for_word()
    {
        using var stream = new MemoryStream(_documents.Render(null, Data("Linha 1\nLinha 2")));
        using var document = WordprocessingDocument.Open(stream, false);

        new OpenXmlValidator().Validate(document, TestContext.Current.CancellationToken).Select(e => e.Description).ShouldBeEmpty();
    }

    [Fact]
    public void A_marker_split_across_runs_is_still_replaced()
    {
        using var template = Docx(["Prezados, ", "{{cliente.", "nome}}", " — ", "{{proposta.titulo}}"], ["{{proposta.total}}"]);

        var (text, _, _) = Read(_documents.Render(template, Data()));

        text.ShouldBe("Prezados, Construtora Horizonte S.A. — Proposta 2026\nR$ 43.234,50");
    }

    [Fact]
    public void Line_breaks_in_the_scope_become_word_breaks()
    {
        using var template = Docx(["{{proposta.escopo}}"]);

        var (text, _, breaks) = Read(_documents.Render(template, Data("Primeira linha\nSegunda linha\r\nTerceira")));

        text.ShouldBe("Primeira linhaSegunda linhaTerceira");
        breaks.ShouldBe(2);
    }

    [Fact]
    public void Without_items_the_item_row_disappears_and_empty_values_stay_blank()
    {
        var (text, rows, _) = Read(_documents.Render(null, Data(scope: null) with { Items = [], ValidUntil = null }));

        rows.Count.ShouldBe(1);
        text.ShouldContain("Válida até: \n");
        text.ShouldNotContain("{{");
    }

    [Fact]
    public void Validation_accepts_known_markers_and_lists_the_unknown_ones()
    {
        using var defaultTemplate = new MemoryStream(_documents.DefaultTemplate());
        using var unknown = Docx(["{{cliente.nome}} {{cliente.cpf}}"], ["{{ processo.numero }}"]);

        _documents.Validate(defaultTemplate).IsSuccess.ShouldBeTrue();
        var error = _documents.Validate(unknown).Error.ShouldNotBeNull();
        error.Code.ShouldBe("crm.template_unknown_marker");
        error.Message.ShouldContain("cliente.cpf, processo.numero");
    }

    [Fact]
    public void Validation_refuses_a_file_that_is_not_a_docx()
    {
        using var garbage = new MemoryStream("isto não é um docx"u8.ToArray());

        _documents.Validate(garbage).Error.ShouldBe(CrmErrors.TemplateInvalid);
    }
}

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Integrations;
using LexFlow.Litigation.Infrastructure.Persistence;
using LexFlow.Web.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Importação de processos por planilha (Etapa 5, PR 5D; decisões de 2026-10-18) pela API real, com os jobs rodados
/// pelo teste (não há Worker): envio, validação, pré-visualização, confirmação, gravação e o relatório. Inclui o
/// critério 2 da Etapa 5: 10 mil linhas com 3% inválidas.
/// </summary>
public sealed class CaseImportTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static readonly string[] Everything = ["cases.read", "cases.create", "cases.edit", "settings.structure.manage", "platform.jobs.read"];

    private static readonly string[] Header =
    [
        "Tipo", "CNJ", "Pasta", "Instância", "Comarca", "Vara", "Área", "Valor_causa", "Distribuição", "Cliente_nome", "Cliente_documento", "Cliente_polo", "Contraria_nome",
    ];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings, replaceServices: s => s.AddSingleton<IFileScanner, CleanScanner>());
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public async ValueTask InitializeAsync()
    {
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);

        // O MigrationService cria o bucket no ambiente real; aqui, a própria fábrica.
        await _factory.Services.GetRequiredService<IObjectStorageProvisioner>().EnsureBucketAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record Office(Guid Organization, Guid User);

    /// <summary>Sem ClamAV nos testes: todo arquivo passa.</summary>
    private sealed class CleanScanner : IFileScanner
    {
        public Task<FileScanResult> ScanAsync(Stream content, CancellationToken cancellationToken) => Task.FromResult(FileScanResult.Clean);
    }

    private async Task<Office> OfficeAsync()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies, Everything);
        await _api.CreateAsync("/api/v1/organization/practice-areas", member.UserId, member.OrganizationId, new { name = "Cível" });
        return new Office(member.OrganizationId, member.UserId);
    }

    /// <summary>Número CNJ com o dígito verificador (módulo 97, Resolução CNJ 65): TJSP, ano 2024.</summary>
    private static string Cnj(int sequence, bool wrongDigits = false)
    {
        var number = (1_000_000 + sequence).ToString(CultureInfo.InvariantCulture);
        var digits = 98 - (int)(BigInteger.Parse(number + "2024826" + "0100" + "00", CultureInfo.InvariantCulture) % 97);
        return $"{number}-{(wrongDigits ? (digits + 1) % 100 : digits):00}.2024.8.26.0100";
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> UploadAsync(Office office, byte[] content, string fileName)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cases/imports") { Content = form };
        request.Headers.Add(TestAuthenticationHandler.UserHeader, office.User.ToString());
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, office.Organization.ToString());
        using var response = await _http.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    private async Task<byte[]> DownloadAsync(Office office, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, office.User.ToString());
        request.Headers.Add(TenantResolutionMiddleware.HeaderName, office.Organization.ToString());
        using var response = await _http.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return await response.Content.ReadAsByteArrayAsync(Ct);
    }

    private async Task<int> CaseCountAsync(Guid organization)
    {
        using var ambient = AmbientContext.Begin(organization, "import-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LitigationDbContext>().Cases.CountAsync(Ct);
    }

    /// <summary>
    /// Critério 2: 10 mil linhas, 300 inválidas (6 defeitos × 50: tipo, CNJ com dígito errado, área inexistente, data
    /// impossível, CPF inválido e CNJ repetido no arquivo). As 9.700 válidas viram processos (a maioria com a pasta
    /// do sistema antigo, uma em cada dez com a pasta gerada) e o relatório lista exatamente as 300 linhas.
    /// </summary>
    [Fact]
    public async Task Ten_thousand_rows_with_three_percent_invalid_import_the_rest_and_report_exactly_the_invalid_ones()
    {
        var office = await OfficeAsync();
        var invalid = new Dictionary<int, string>();
        var csv = new StringBuilder(string.Join(';', Header)).Append('\n');
        for (var i = 0; i < 10_000; i++)
        {
            string[] cells =
            [
                "judicial", Cnj(i), i % 10 == 0 ? string.Empty : $"LEG-{i:00000}", "1", "São Paulo", $"{(i % 9) + 1}ª Vara Cível", "Cível",
                "1.234,56", "10/01/2024", $"Cliente {i % 40}", string.Empty, "passivo", $"Parte Contrária {i % 100}",
            ];
            if (i >= 17 && (i - 17) % 33 == 0 && (i - 17) / 33 < 300)
            {
                var defect = (i - 17) / 33 % 6;
                invalid[i + 2] = defect switch
                {
                    0 => Set(cells, 0, "trabalhista", "litigation.import_kind_invalid"),
                    1 => Set(cells, 1, Cnj(i, wrongDigits: true), "litigation.cnj_invalid"),
                    2 => Set(cells, 6, "Área Inexistente", "litigation.practice_area_invalid"),
                    3 => Set(cells, 8, "31/02/2024", "litigation.import_date_invalid"),
                    4 => Set(cells, 10, "111.111.111-11", "litigation.import_document_invalid"),
                    _ => Set(cells, 1, Cnj(i - 1), "litigation.import_duplicate_in_file"),
                };
            }

            csv.Append(string.Join(';', cells)).Append('\n');
        }

        var (uploadStatus, started) = await UploadAsync(office, Encoding.UTF8.GetBytes(csv.ToString()), "carteira.csv");
        uploadStatus.ShouldBe(HttpStatusCode.Accepted, started.ToString());
        var importId = started.GetProperty("importId").GetGuid();
        await _factory.RunJobsAsync(office.Organization);
        var (_, validated) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/cases/imports/{importId}", office.User, office.Organization);
        var (confirmStatus, _) = await _api.SendAsync(HttpMethod.Post, $"/api/v1/cases/imports/{importId}/confirm", office.User, office.Organization);
        await _factory.RunJobsAsync(office.Organization);
        var (_, done) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/cases/imports/{importId}", office.User, office.Organization);
        var (_, job) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/jobs/{started.GetProperty("jobId").GetGuid()}", office.User, office.Organization);

        validated.GetProperty("status").GetString().ShouldBe("ready");
        (validated.GetProperty("totalRows").GetInt32(), validated.GetProperty("validRows").GetInt32(), validated.GetProperty("invalidRows").GetInt32()).ShouldBe((10_000, 9_700, 300));
        validated.GetProperty("preview").GetArrayLength().ShouldBe(20);
        confirmStatus.ShouldBe(HttpStatusCode.Accepted);
        done.GetProperty("status").GetString().ShouldBe("completed", done.ToString());
        (done.GetProperty("totalRows").GetInt32(), done.GetProperty("validRows").GetInt32(), done.GetProperty("invalidRows").GetInt32()).ShouldBe((10_000, 9_700, 300));
        done.GetProperty("importedRows").GetInt32().ShouldBe(9_700);
        job.GetProperty("status").GetString().ShouldBe("succeeded");
        (await CaseCountAsync(office.Organization)).ShouldBe(9_700);

        using var report = new XLWorkbook(new MemoryStream(await DownloadAsync(office, $"/api/v1/cases/imports/{importId}/errors")));
        var lines = report.Worksheet(1).RowsUsed().Skip(1)
            .Select(r => (Row: (int)r.Cell(1).GetDouble(), Code: r.Cell(4).GetString()))
            .ToList();
        lines.Select(l => l.Row).Distinct().Order().ShouldBe(invalid.Keys.Order());
        lines.ShouldAllBe(l => invalid[l.Row] == l.Code);

        static string Set(string[] cells, int column, string value, string code)
        {
            cells[column] = value;
            return code;
        }
    }

    [Fact]
    public async Task An_xlsx_with_real_cells_imports_and_the_folders_follow_the_rules()
    {
        var office = await OfficeAsync();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Processos");
        string[] header = ["tipo", "cnj", "pasta", "valor_causa", "distribuicao", "cliente_nome", "cliente_documento", "contraria_nome"];
        for (var c = 0; c < header.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = header[c];
        }

        object[][] rows =
        [
            ["judicial", Cnj(1), "ANTIGA-001", 150000.5, new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Unspecified), "Banco Alfa S.A.", "11.444.777/0001-61", "José da Silva"],
            ["extrajudicial", string.Empty, string.Empty, 1000, new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Unspecified), "Banco Alfa S.A.", "11444777000161", string.Empty],
            ["administrativo", string.Empty, "ANTIGA-001", 10, string.Empty, "Outra Ltda.", string.Empty, string.Empty],
        ];
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = rows[r][c] switch
                {
                    double d => d,
                    int n => n,
                    DateTime date => date,
                    var other => (string)other,
                };
            }
        }

        using var buffer = new MemoryStream();
        workbook.SaveAs(buffer);

        var (status, started) = await UploadAsync(office, buffer.ToArray(), "carteira.xlsx");
        status.ShouldBe(HttpStatusCode.Accepted, started.ToString());
        var importId = started.GetProperty("importId").GetGuid();
        var (earlyStatus, early) = await _api.SendAsync(HttpMethod.Post, $"/api/v1/cases/imports/{importId}/confirm", office.User, office.Organization);
        await _factory.RunJobsAsync(office.Organization);
        await _api.SendAsync(HttpMethod.Post, $"/api/v1/cases/imports/{importId}/confirm", office.User, office.Organization);
        var (againStatus, again) = await _api.SendAsync(HttpMethod.Post, $"/api/v1/cases/imports/{importId}/confirm", office.User, office.Organization);
        await _factory.RunJobsAsync(office.Organization);
        var (_, done) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/cases/imports/{importId}", office.User, office.Organization);
        var (_, cases) = await _api.SendAsync(HttpMethod.Get, "/api/v1/cases?limit=10", office.User, office.Organization);

        earlyStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(early).ShouldBe("litigation.import_not_ready");
        againStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(again).ShouldBe("litigation.import_not_ready");
        done.GetProperty("importedRows").GetInt32().ShouldBe(2, done.ToString());
        done.GetProperty("errors").EnumerateArray().Select(e => (e.GetProperty("row").GetInt32(), e.GetProperty("code").GetString()))
            .ShouldBe([(4, "litigation.import_duplicate_in_file")]);
        var items = cases.GetProperty("items").EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        items.Select(c => c.GetProperty("internalFolder").GetString()).ShouldContain("ANTIGA-001");
        items.Single(c => c.GetProperty("internalFolder").GetString() != "ANTIGA-001").GetProperty("internalFolder").GetString()!.ShouldEndWith("-00001");
    }

    [Fact]
    public async Task Broken_files_are_refused_on_upload_and_the_template_has_every_column()
    {
        var office = await OfficeAsync();

        var (noHeaderStatus, noHeader) = await UploadAsync(office, Encoding.UTF8.GetBytes("cnj;pasta\n123;X\n"), "carteira.csv");
        var (wrongTypeStatus, wrongType) = await UploadAsync(office, Encoding.UTF8.GetBytes("tipo;cliente_nome\n"), "carteira.txt");
        var (emptyStatus, _) = await UploadAsync(office, [], "carteira.csv");
        var (missingStatus, _) = await _api.SendAsync(HttpMethod.Get, $"/api/v1/cases/imports/{Guid.CreateVersion7()}", office.User, office.Organization);
        using var template = new XLWorkbook(new MemoryStream(await DownloadAsync(office, "/api/v1/cases/imports/template")));

        noHeaderStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(noHeader).ShouldBe("litigation.import_header_missing");
        wrongTypeStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(wrongType).ShouldBe("litigation.import_file_invalid");
        emptyStatus.ShouldBe(HttpStatusCode.BadRequest);
        missingStatus.ShouldBe(HttpStatusCode.NotFound);
        template.Worksheet(1).Row(1).CellsUsed().Select(c => c.GetString()).ShouldContain("cliente_nome");
        template.Worksheet(1).Row(1).CellsUsed().Count().ShouldBe(23);
    }
}

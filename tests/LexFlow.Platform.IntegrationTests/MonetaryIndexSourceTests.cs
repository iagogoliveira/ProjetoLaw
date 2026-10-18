using System.Net;
using System.Text;
using LexFlow.Platform.Application.Indexes;
using LexFlow.Platform.Domain.Indexes;
using LexFlow.Platform.Infrastructure.Indexes;

namespace LexFlow.Platform.IntegrationTests;

/// <summary>
/// Fontes oficiais dos índices sem internet: um <see cref="HttpMessageHandler"/> falso devolve respostas no formato real
/// de cada API (amostras de 2026-10-18) e guarda a URL pedida.
/// </summary>
public sealed class MonetaryIndexSourceTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly DateOnly August = new(2026, 8, 1);
    private static readonly DateOnly September = new(2026, 9, 1);

    [Fact]
    public async Task Ibge_reads_the_month_column_from_the_header_and_skips_months_without_value()
    {
        const string body = """
            [
              {"NC":"Nível Territorial (Código)","V":"Valor","D1C":"Brasil (Código)","D2C":"Variável (Código)","D3C":"Mês (Código)"},
              {"NC":"1","V":"0.62","D1C":"1","D2C":"355","D3C":"202608"},
              {"NC":"1","V":"...","D1C":"1","D2C":"355","D3C":"202609"}
            ]
            """;
        var handler = new FakeHandler(body);
        var source = new IbgeSidraIndexSource(Client(handler, "https://apisidra.ibge.gov.br/"));

        var values = await source.FetchAsync(MonetaryIndexKind.IpcaE, August, September, Ct);

        values.ShouldBe([new IndexValue(August, 0.62m)]);
        handler.LastUri.ShouldBe(new Uri("https://apisidra.ibge.gov.br/values/t/3065/n1/all/v/355/p/202608-202609"));
        source.Supports(MonetaryIndexKind.Inpc).ShouldBeTrue();
        source.Supports(MonetaryIndexKind.Selic).ShouldBeFalse();
        await source.FetchAsync(MonetaryIndexKind.Inpc, August, September, Ct);
        handler.LastUri!.AbsolutePath.ShouldBe("/values/t/1736/n1/all/v/44/p/202608-202609");
    }

    [Fact]
    public async Task Ibge_without_the_month_column_is_a_format_error()
    {
        var source = new IbgeSidraIndexSource(Client(new FakeHandler("""[{"V":"Valor"},{"V":"0.5"}]"""), "https://apisidra.ibge.gov.br/"));

        await Should.ThrowAsync<FormatException>(() => source.FetchAsync(MonetaryIndexKind.Inpc, August, September, Ct));
        (await new IbgeSidraIndexSource(Client(new FakeHandler("[]"), "https://apisidra.ibge.gov.br/")).FetchAsync(MonetaryIndexKind.Inpc, August, September, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Bcb_asks_the_series_by_date_range_and_reads_the_brazilian_dates()
    {
        var handler = new FakeHandler("""[{"data":"01/08/2026","valor":"1.09"},{"data":"01/09/2026","valor":"1.08"}]""");
        var source = new BcbSgsIndexSource(Client(handler, "https://api.bcb.gov.br/"));

        var values = await source.FetchAsync(MonetaryIndexKind.Selic, August, September, Ct);

        values.ShouldBe([new IndexValue(August, 1.09m), new IndexValue(September, 1.08m)]);
        handler.LastUri!.PathAndQuery.ShouldBe("/dados/serie/bcdata.sgs.4390/dados?formato=json&dataInicial=01/08/2026&dataFinal=30/09/2026");
        await source.FetchAsync(MonetaryIndexKind.Tr, August, September, Ct);
        handler.LastUri!.AbsolutePath.ShouldBe("/dados/serie/bcdata.sgs.7811/dados");
        source.Supports(MonetaryIndexKind.IpcaE).ShouldBeFalse();
    }

    [Fact]
    public async Task Bcb_rejection_page_or_error_status_makes_the_importer_try_the_next_source()
    {
        // O BCB devolve uma página HTML com status 200 quando recusa a requisição.
        var html = new BcbSgsIndexSource(Client(new FakeHandler("<html>Requisição inválida!</html>"), "https://api.bcb.gov.br/"));
        await Should.ThrowAsync<System.Text.Json.JsonException>(() => html.FetchAsync(MonetaryIndexKind.Selic, August, September, Ct));

        var down = new BcbSgsIndexSource(Client(new FakeHandler("{}", HttpStatusCode.ServiceUnavailable), "https://api.bcb.gov.br/"));
        await Should.ThrowAsync<HttpRequestException>(() => down.FetchAsync(MonetaryIndexKind.Selic, August, September, Ct));

        var notAList = new BcbSgsIndexSource(Client(new FakeHandler("""{"erro":"x"}"""), "https://api.bcb.gov.br/"));
        await Should.ThrowAsync<FormatException>(() => notAList.FetchAsync(MonetaryIndexKind.Selic, August, September, Ct));
    }

    [Fact]
    public async Task Ipeadata_filters_the_whole_series_to_the_requested_months()
    {
        const string body = """
            {"value":[
              {"SERCODIGO":"BM12_TJTR12","VALDATA":"2026-07-01T00:00:00-03:00","VALVALOR":0.17},
              {"SERCODIGO":"BM12_TJTR12","VALDATA":"2026-08-01T00:00:00-03:00","VALVALOR":0.1712},
              {"SERCODIGO":"BM12_TJTR12","VALDATA":"2026-09-01T00:00:00-03:00","VALVALOR":null},
              {"SERCODIGO":"BM12_TJTR12","VALDATA":"2026-10-01T00:00:00-03:00","VALVALOR":0.1616}
            ]}
            """;
        var handler = new FakeHandler(body);
        var source = new IpeadataIndexSource(Client(handler, "https://www.ipeadata.gov.br/"));

        var values = await source.FetchAsync(MonetaryIndexKind.Tr, August, September, Ct);

        values.ShouldBe([new IndexValue(August, 0.1712m)]);
        handler.LastUri!.ToString().ShouldBe("https://www.ipeadata.gov.br/api/odata4/ValoresSerie(SERCODIGO='BM12_TJTR12')");
        await Should.ThrowAsync<FormatException>(() =>
            new IpeadataIndexSource(Client(new FakeHandler("[]"), "https://www.ipeadata.gov.br/")).FetchAsync(MonetaryIndexKind.Selic, August, September, Ct));
    }

    [Fact]
    public async Task The_fake_source_has_every_month_with_a_stable_value()
    {
        var source = new FakeIndexSource();

        var values = await source.FetchAsync(MonetaryIndexKind.Selic, August, September, Ct);

        source.Supports(MonetaryIndexKind.Tr).ShouldBeTrue();
        values.ShouldBe([new IndexValue(August, 0.92m), new IndexValue(September, 0.90m)]);
        FakeIndexSource.PercentFor(MonetaryIndexKind.IpcaE, August).ShouldBe(0.42m);
        FakeIndexSource.PercentFor(MonetaryIndexKind.Inpc, August).ShouldBe(0.37m);
        FakeIndexSource.PercentFor(MonetaryIndexKind.Tr, August).ShouldBe(0.12m);
    }

    private static HttpClient Client(FakeHandler handler, string baseAddress) => new(handler) { BaseAddress = new Uri(baseAddress) };

    private sealed class FakeHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}

using System.Net;
using System.Text.Json.Nodes;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Infrastructure.Movements;

namespace LexFlow.Publications.IntegrationTests;

public sealed class DataJudContractTests
{
    private const string Cnj = "22380589820258268434";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Fixture => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "datajud-page.json"));
    [Fact]
    public async Task Invalid_cnj_cursor_key_and_unknown_tribunal_never_call_http()
    {
        using var handler = new RecordedHandler(Fixture);
        using var http = new HttpClient(handler);
        using var throttle = new DataJudThrottle();
        var provider = new DataJudProvider(http, new Courts(), throttle);
        (await provider.FetchAsync(new("invalid", null), "key", Ct)).Error!.Code.ShouldBe("cnj.invalid");
        (await provider.FetchAsync(new(Cnj, "{\"Tribunal\":\"tjsp\",\"Sort\":null}"), "key", Ct)).Error!.Code.ShouldBe("publications.datajud_cursor_invalid");
        (await provider.FetchAsync(new(Cnj, null), "bad key", Ct)).Error!.Code.ShouldBe("publications.credential_rejected");
        (await new DataJudProvider(http, new Courts("unknown"), throttle).FetchAsync(new(Cnj, null), "key", Ct))
            .Error!.Code.ShouldBe("publications.tribunal_unsupported");
        handler.Url.ShouldBeNull();
    }
    [Fact]
    public async Task Provider_limits_simultaneous_requests_and_maps_network_failures()
    {
        using var handler = new GatedHandler();
        using var http = new HttpClient(handler);
        using var throttle = new DataJudThrottle();
        var provider = new DataJudProvider(http, new Courts(), throttle);
        var requests = Enumerable.Range(0, 8).Select(_ => provider.FetchAsync(new(Cnj, null), "key", Ct)).ToArray();
        await handler.FirstBatch.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        handler.Calls.ShouldBe(4);
        handler.Release.SetResult();
        var results = await Task.WhenAll(requests);
        results.All(r => r.Error?.Code is "publications.provider_unavailable").ShouldBeTrue();
        handler.Maximum.ShouldBe(4);
    }
    [Fact]
    public async Task Official_contract_imports_history_and_deduplicates_reordered_complements()
    {
        using var handler = new RecordedHandler(Fixture);
        using var http = new HttpClient(handler);
        using var throttle = new DataJudThrottle();
        var provider = new DataJudProvider(http, new Courts(), throttle);
        var first = await provider.FetchAsync(new(Cnj, null), "test-key", Ct);
        first.IsSuccess.ShouldBeTrue(first.Error?.Code);
        first.Value.Items.Count.ShouldBe(2);
        first.Value.Items[0].OccurredAt.Year.ShouldBe(2020);
        handler.Url.ShouldBe("https://api-publica.datajud.cnj.jus.br/api_publica_tjsp/_search");
        handler.Authorization.ShouldBe("APIKey test-key");
        handler.Body.ShouldContain(Cnj);
        handler.Body.ShouldContain("id.keyword");
        var json = JsonNode.Parse(Fixture)!;
        var complements = json["hits"]!["hits"]![0]!["_source"]!["movimentos"]![0]!["complementosTabelados"]!.AsArray();
        var last = complements[1]!.DeepClone();
        complements.RemoveAt(1);
        complements.Insert(0, last);
        json["hits"]!["hits"]![0]!["_source"]!["movimentos"]![0]!["nome"] = "Distribuição com rótulo atualizado";
        complements[0]!["nome"] = "Rótulo do complemento atualizado";
        handler.Json = json.ToJsonString();
        var next = await provider.FetchAsync(new(Cnj, null), "another-key", Ct);
        next.Value.Items.Select(i => i.ExternalId).ShouldBe(first.Value.Items.Select(i => i.ExternalId));
        handler.Authorization.ShouldBe("APIKey another-key");
        http.DefaultRequestHeaders.Authorization.ShouldBeNull();
    }
    [Fact]
    public async Task Full_page_returns_checkpoint_and_next_request_sends_search_after()
    {
        var json = JsonNode.Parse(Fixture)!;
        var hits = json["hits"]!["hits"]!.AsArray();
        var template = hits[0]!.DeepClone();
        for (var i = 1; i < DataJudProvider.PageSize; i++) { hits.Add(template.DeepClone()); }
        using var handler = new RecordedHandler(json.ToJsonString());
        using var http = new HttpClient(handler);
        using var throttle = new DataJudThrottle();
        var provider = new DataJudProvider(http, new Courts(), throttle);
        var page = (await provider.FetchAsync(new(Cnj, null), "key", Ct)).Value;
        page.NextCursor.ShouldNotBeNull();
        handler.Json = Fixture;
        (await provider.FetchAsync(new(Cnj, page.NextCursor), "key", Ct)).Value.NextCursor.ShouldBeNull();
        handler.Body.ShouldContain("search_after");
        (await provider.FetchAsync(new(Cnj, "{}"), "key", Ct)).Error!.Code.ShouldBe("publications.datajud_cursor_invalid");
        (await provider.FetchAsync(new(Cnj, "invalid"), "key", Ct)).IsFailure.ShouldBeTrue();
    }
    [Theory]
    [InlineData(401, "publications.credential_rejected")]
    [InlineData(403, "publications.credential_rejected")]
    [InlineData(429, "publications.provider_unavailable")]
    [InlineData(503, "publications.provider_unavailable")]
    public async Task Http_failures_return_stable_errors(int status, string code)
    {
        using var handler = new RecordedHandler("secret-must-not-be-returned") { Status = (HttpStatusCode)status };
        using var http = new HttpClient(handler);
        using var throttle = new DataJudThrottle();
        var result = await new DataJudProvider(http, new Courts(), throttle).FetchAsync(new(Cnj, null), "key", Ct);
        result.Error!.Code.ShouldBe(code);
        result.Error.Message.ShouldNotContain("secret");
    }
    [Theory]
    [InlineData("timed_out")]
    [InlineData("sigilo")]
    [InlineData("cnj")]
    [InlineData("shards")]
    [InlineData("malformed")]
    [InlineData("date")]
    public async Task Incomplete_private_or_unexpected_documents_are_not_imported(string mutation)
    {
        var json = JsonNode.Parse(Fixture)!;
        if (string.Equals(mutation, "timed_out", StringComparison.Ordinal)) { json["timed_out"] = true; }
        if (string.Equals(mutation, "sigilo", StringComparison.Ordinal)) { json["hits"]!["hits"]![0]!["_source"]!["nivelSigilo"] = 1; }
        if (string.Equals(mutation, "cnj", StringComparison.Ordinal)) { json["hits"]!["hits"]![0]!["_source"]!["numeroProcesso"] = "other"; }
        if (string.Equals(mutation, "shards", StringComparison.Ordinal)) { json["_shards"]!["failed"] = 1; }
        if (string.Equals(mutation, "date", StringComparison.Ordinal)) { json["hits"]!["hits"]![0]!["_source"]!["movimentos"]![0]!["dataHora"] = null; }
        using var handler = new RecordedHandler(string.Equals(mutation, "malformed", StringComparison.Ordinal) ? "invalid" : json.ToJsonString());
        using var http = new HttpClient(handler);
        using var throttle = new DataJudThrottle();
        (await new DataJudProvider(http, new Courts(), throttle).FetchAsync(new(Cnj, null), "key", Ct)).Error!.Code.ShouldBe("publications.datajud_response_invalid");
    }
    private sealed class Courts(string acronym = "TJSP") : ICourtDirectory
    {
        public Task<CourtInfo?> FindAsync(int segment, string code, CancellationToken cancellationToken) =>
            Task.FromResult<CourtInfo?>(new(Guid.Empty, segment, code, acronym, "Tribunal", "SP"));
        public Task<CourtInfo?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<CourtInfo?>(null);
    }
    private sealed class GatedHandler : HttpMessageHandler
    {
        public TaskCompletionSource FirstBatch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _calls;
        private int _maximum;
        public int Calls => Volatile.Read(ref _calls);
        public int Maximum => Volatile.Read(ref _maximum);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            RecordMaximum(active);
            if (Interlocked.Increment(ref _calls) == 4) { FirstBatch.TrySetResult(); }
            await Release.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref _active);
            throw new HttpRequestException("Synthetic failure");
        }
        private void RecordMaximum(int value)
        {
            int old;
            do { old = Volatile.Read(ref _maximum); if (old >= value) { return; } }
            while (Interlocked.CompareExchange(ref _maximum, value, old) != old);
        }
    }
    private sealed class RecordedHandler(string json) : HttpMessageHandler
    {
        public string Json { get; set; } = json;
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public string? Url { get; private set; }
        public string? Authorization { get; private set; }
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.AbsoluteUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(Status) { Content = new StringContent(Json) };
        }
    }
}

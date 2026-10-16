using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using LexFlow.Application.Abstractions.Idempotency;
using LexFlow.Web.Common.Idempotency;
using LexFlow.Web.Common.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.UnitTests.Web;

/// <summary>Armazenamento em memória com a mesma semântica do <c>PlatformIdempotencyStore</c>.</summary>
internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, (string Hash, StoredResponse? Response)> _entries = new(StringComparer.Ordinal);

    public Task<IdempotencyBegin> BeginAsync(string key, string requestHash, CancellationToken cancellationToken)
    {
        if (_entries.TryAdd(key, (requestHash, null)))
        {
            return Task.FromResult(new IdempotencyBegin(IdempotencyOutcome.Started));
        }

        var existing = _entries[key];
        return Task.FromResult(
            !string.Equals(existing.Hash, requestHash, StringComparison.Ordinal) ? new IdempotencyBegin(IdempotencyOutcome.KeyReused)
            : existing.Response is null ? new IdempotencyBegin(IdempotencyOutcome.InProgress)
            : new IdempotencyBegin(IdempotencyOutcome.Replay, existing.Response));
    }

    public Task CompleteAsync(string key, StoredResponse response, CancellationToken cancellationToken)
    {
        _entries[key] = (_entries[key].Hash, response);
        return Task.CompletedTask;
    }

    public Task AbandonAsync(string key, CancellationToken cancellationToken)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public void MarkInProgress(string key, string hash) => _entries[key] = (hash, null);
}

public sealed class IdempotencyFilterTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly InMemoryIdempotencyStore _store = new();
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private int _executions;
    private int _failures;

    public async ValueTask InitializeAsync()
    {
        _app = await TestWebHost.StartAsync(app =>
        {
            app.MapPost("/invoices/{id}/issue", (Guid id, IssueRequest request) =>
                {
                    Interlocked.Increment(ref _executions);
                    return TypedResults.Created($"/invoices/{id}", new { id, request.Amount, execution = _executions });
                })
                .WithIdempotency();
            app.MapPost("/flaky", IResult () => Interlocked.Increment(ref _failures) == 1
                    ? TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable)
                    : TypedResults.Ok("ok"))
                .WithIdempotency();
            app.MapGet("/jobs-accepted", () => JobResults.Accepted(Guid.Parse("0199f0a0-0000-7000-8000-000000000001")));
        }, configureServices: services => services.AddSingleton<IIdempotencyStore>(_store));
        _client = _app.GetTestClient();
    }

    private sealed record IssueRequest(decimal Amount);

    private async Task<HttpResponseMessage> PostAsync(string path, string? key, string json = """{"amount":100.5}""")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (key is not null)
        {
            request.Headers.Add(IdempotencyEndpointExtensions.HeaderName, key);
        }

        return await _client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Repeating_with_same_key_and_body_replays_the_first_response_without_executing_again()
    {
        var path = $"/invoices/{Guid.CreateVersion7()}/issue";

        using var first = await PostAsync(path, "emitir-1");
        using var second = await PostAsync(path, "emitir-1");

        _executions.ShouldBe(1);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await second.Content.ReadAsStringAsync(Ct)).ShouldBe(await first.Content.ReadAsStringAsync(Ct));
        second.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        second.Headers.GetValues(IdempotencyEndpointExtensions.ReplayedHeader).Single().ShouldBe("true");
        first.Headers.Contains(IdempotencyEndpointExtensions.ReplayedHeader).ShouldBeFalse();
    }

    [Fact]
    public async Task Same_key_with_different_body_is_rejected()
    {
        var path = $"/invoices/{Guid.CreateVersion7()}/issue";
        await PostAsync(path, "emitir-2");

        using var response = await PostAsync(path, "emitir-2", """{"amount":999}""");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ShouldHaveCodeAsync(response, "idempotency.key_reused");
        _executions.ShouldBe(1);
    }

    [Fact]
    public async Task Same_key_while_first_request_is_running_returns_409()
    {
        var path = $"/invoices/{Guid.CreateVersion7()}/issue";
        // Mesmo hash que o filtro calcularia para a requisição abaixo.
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = path;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"amount":100.5}"""));
        _store.MarkInProgress("emitir-3", await IdempotencyHash.ComputeAsync(context.Request));

        using var response = await PostAsync(path, "emitir-3");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldHaveCodeAsync(response, "idempotency.in_progress");
        _executions.ShouldBe(0);
    }

    [Fact]
    public async Task Without_key_every_request_executes()
    {
        var path = $"/invoices/{Guid.CreateVersion7()}/issue";

        await PostAsync(path, key: null);
        await PostAsync(path, key: null);

        _executions.ShouldBe(2);
    }

    [Theory]
    [InlineData("chave com espaço")]
    [InlineData("chave/barra")]
    public async Task Invalid_key_is_rejected(string key)
    {
        using var response = await PostAsync($"/invoices/{Guid.CreateVersion7()}/issue", key);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldHaveCodeAsync(response, "idempotency.invalid_key");
    }

    [Fact]
    public async Task Key_longer_than_128_characters_is_rejected()
    {
        using var response = await PostAsync($"/invoices/{Guid.CreateVersion7()}/issue", new string('a', 129));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Server_error_releases_the_key_so_the_client_can_retry()
    {
        using var failed = await PostAsync("/flaky", "tentar-de-novo");
        using var retried = await PostAsync("/flaky", "tentar-de-novo");

        failed.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
        retried.Headers.Contains(IdempotencyEndpointExtensions.ReplayedHeader).ShouldBeFalse();
    }

    [Fact]
    public async Task Long_operation_returns_202_with_job_location()
    {
        using var response = await _client.GetAsync(new Uri("/jobs-accepted", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().ShouldBe("/api/v1/jobs/0199f0a0-0000-7000-8000-000000000001");
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("status").GetString().ShouldBe("queued");
    }

    private static async Task ShouldHaveCodeAsync(HttpResponseMessage response, string code) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("code").GetString().ShouldBe(code);

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Publications.Contracts;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Publications.Infrastructure.Movements;

public sealed class DataJudThrottle : IDisposable
{
    internal SemaphoreSlim Gate { get; } = new(4, 4);
    public void Dispose() => Gate.Dispose();
}

public sealed class DataJudProvider(HttpClient http, LexFlow.Platform.Contracts.Courts.ICourtDirectory courts, DataJudThrottle throttle) : IMovementPageProvider
{
    public const int PageSize = 100;
    internal static readonly Error InvalidResponse = Error.BusinessRule("publications.datajud_response_invalid", "Resposta DataJud inválida ou incompleta.");
    private static readonly Error InvalidCursor = Error.Validation("publications.datajud_cursor_invalid", "Cursor DataJud inválido.");
    private readonly DataJudTribunals _tribunals = new(courts);

    public async Task<Result<MovementPage>> FetchAsync(MovementQuery query, string apiKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var cnj = CnjNumber.Create(query.Cnj);
        if (cnj.IsFailure) { return cnj.Error; }
        var tribunal = await _tribunals.ResolveAsync(cnj.Value, cancellationToken);
        if (tribunal.IsFailure) { return tribunal.Error; }
        var body = BuildQuery(cnj.Value.Digits, tribunal.Value, query.Cursor);
        if (body.IsFailure) { return body.Error; }
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Any(char.IsWhiteSpace)) { return CredentialError(); }
        await throttle.Gate.WaitAsync(cancellationToken);
        try { return await SendAsync(tribunal.Value, cnj.Value.Digits, apiKey, body.Value, cancellationToken); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Error.BusinessRule("publications.provider_unavailable", "DataJud indisponível.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or OverflowException)
        {
            return InvalidResponse;
        }
        finally { throttle.Gate.Release(); }
    }

    private async Task<Result<MovementPage>> SendAsync(string tribunal, string cnj, string apiKey, Dictionary<string, object> body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("https://api-publica.datajud.cnj.jus.br/api_publica_" + tribunal + "/_search"));
        request.Headers.Authorization = new AuthenticationHeaderValue("APIKey", apiKey);
        request.Content = JsonContent.Create(body);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) { return CredentialError(); }
        if (!response.IsSuccessStatusCode) { return Error.BusinessRule("publications.provider_unavailable", "DataJud indisponível."); }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return DataJudResponse.Read(document.RootElement, tribunal, cnj);
    }

    private static Result<Dictionary<string, object>> BuildQuery(string cnj, string tribunal, string? cursor)
    {
        var body = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["size"] = PageSize,
            ["query"] = new { match = new { numeroProcesso = cnj } },
            ["sort"] = new object[] { new Dictionary<string, object>(StringComparer.Ordinal) { ["@timestamp"] = new { order = "asc" } },
                new Dictionary<string, object>(StringComparer.Ordinal) { ["id.keyword"] = new { order = "asc" } } },
        };
        if (cursor is null) { return body; }
        try
        {
            if (cursor.Length > 4096) { return InvalidCursor; }
            var position = JsonSerializer.Deserialize<DataJudCursor>(cursor);
            if (position is null || !string.Equals(position.Tribunal, tribunal, StringComparison.Ordinal)
                || !string.Equals(position.Cnj, cnj, StringComparison.Ordinal) || position.Sort is not { Length: 2 }
                || position.Sort.Any(s => s.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))) { return InvalidCursor; }
            body["search_after"] = position.Sort;
            return body;
        }
        catch (JsonException) { return InvalidCursor; }
    }
    private static Error CredentialError() => Error.BusinessRule("publications.credential_rejected", "Chave DataJud ausente ou recusada.");
}

internal sealed record DataJudCursor(string Tribunal, string Cnj, JsonElement[] Sort);

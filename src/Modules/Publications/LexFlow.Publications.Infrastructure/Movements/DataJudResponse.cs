using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LexFlow.Publications.Contracts;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Publications.Infrastructure.Movements;

internal static class DataJudResponse
{
    public static Result<MovementPage> Read(JsonElement root, string tribunal, string cnj)
    {
        if (root.GetProperty("timed_out").GetBoolean() || root.GetProperty("_shards").GetProperty("failed").GetInt32() != 0)
        { return DataJudProvider.InvalidResponse; }
        var hits = root.GetProperty("hits").GetProperty("hits");
        if (hits.GetArrayLength() > DataJudProvider.PageSize) { return DataJudProvider.InvalidResponse; }
        var items = new Dictionary<string, CapturedMovement>(StringComparer.Ordinal);
        JsonElement[]? lastSort = null;
        foreach (var hit in hits.EnumerateArray())
        {
            var source = hit.GetProperty("_source");
            if (!string.Equals(source.GetProperty("numeroProcesso").GetString(), cnj, StringComparison.Ordinal)
                || source.GetProperty("nivelSigilo").GetInt32() != 0
                || !string.Equals(source.GetProperty("tribunal").GetString(), tribunal, StringComparison.OrdinalIgnoreCase))
            { return DataJudProvider.InvalidResponse; }
            var grade = source.GetProperty("grau").GetString() ?? throw new FormatException();
            foreach (var movement in source.GetProperty("movimentos").EnumerateArray())
            {
                var item = ReadMovement(movement, tribunal, grade);
                items[item.ExternalId] = item;
            }
            lastSort = hit.GetProperty("sort").EnumerateArray().Select(e => e.Clone()).ToArray();
            if (lastSort.Length != 2 || lastSort.Any(s => s.ValueKind is not (JsonValueKind.String or JsonValueKind.Number)))
            { return DataJudProvider.InvalidResponse; }
        }
        var next = hits.GetArrayLength() == DataJudProvider.PageSize ? JsonSerializer.Serialize(new DataJudCursor(tribunal, cnj, lastSort!)) : null;
        return new MovementPage(tribunal, items.Values.OrderBy(m => m.OccurredAt).ToList(), next);
    }

    private static CapturedMovement ReadMovement(JsonElement movement, string tribunal, string grade)
    {
        if (!DateTimeOffset.TryParse(movement.GetProperty("dataHora").GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var occurredAt))
        { throw new FormatException(); }
        var date = occurredAt.ToUniversalTime();
        var code = movement.GetProperty("codigo").GetInt32();
        var name = movement.GetProperty("nome").GetString()?.Trim();
        if (code <= 0 || string.IsNullOrWhiteSpace(name)) { throw new FormatException(); }
        var complements = movement.TryGetProperty("complementosTabelados", out var values) ? values.EnumerateArray()
            .Select(c => JsonSerializer.Serialize(new
            {
                code = c.GetProperty("codigo").GetInt32(),
                value = c.GetProperty("valor").ToString(),
                name = c.GetProperty("nome").GetString(),
                description = c.GetProperty("descricao").GetString()
            }))
            .Order(StringComparer.Ordinal).ToArray() : [];
        var organ = movement.TryGetProperty("orgaoJulgador", out var court) && court.TryGetProperty("codigoOrgao", out var organCode) ? organCode.ToString() : string.Empty;
        var description = name + (complements.Length == 0 ? string.Empty : " — " + string.Join("; ", complements.Select(ComplementDescription)));
        if (description.Length > 20_000) { throw new FormatException(); }
        // Rótulos da TPU podem ser corrigidos sem representar um novo ato.
        var identities = complements.Select(ComplementIdentity).Order(StringComparer.Ordinal).ToArray();
        var canonical = JsonSerializer.Serialize(new { tribunal, grade, date, code, organ, complements = identities });
        var id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new CapturedMovement(id, date, description);
    }
    private static string ComplementDescription(string value)
    {
        using var json = JsonDocument.Parse(value);
        return json.RootElement.GetProperty("description").GetString() + ": " + json.RootElement.GetProperty("name").GetString();
    }
    private static string ComplementIdentity(string value)
    {
        using var json = JsonDocument.Parse(value);
        return JsonSerializer.Serialize(new { code = json.RootElement.GetProperty("code").GetInt32(), value = json.RootElement.GetProperty("value").GetString() });
    }
}

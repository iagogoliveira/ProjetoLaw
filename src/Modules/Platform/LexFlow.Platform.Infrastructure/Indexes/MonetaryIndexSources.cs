using System.Globalization;
using System.Text.Json;
using LexFlow.Platform.Application.Indexes;
using LexFlow.Platform.Domain.Indexes;

namespace LexFlow.Platform.Infrastructure.Indexes;

/// <summary>Configuração da importação dos índices (seção <c>Platform:MonetaryIndexes</c>).</summary>
public sealed class MonetaryIndexSettings
{
    public const string SectionName = "Platform:MonetaryIndexes";

    /// <summary><c>Official</c> (padrão: IBGE, BCB e Ipeadata) ou <c>Fake</c> (dev e testes, sem internet).</summary>
    public string Source { get; init; } = "Official";

    public Uri IbgeBaseAddress { get; init; } = new("https://apisidra.ibge.gov.br/");

    public Uri BcbBaseAddress { get; init; } = new("https://api.bcb.gov.br/");

    public Uri IpeadataBaseAddress { get; init; } = new("https://www.ipeadata.gov.br/");
}

/// <summary>
/// IBGE, API SIDRA: IPCA-E pela variação mensal do IPCA-15 (tabela 3065, variável 355) e INPC (tabela 1736,
/// variável 44). A resposta é uma lista em que a primeira linha descreve as colunas; "..." e "-" são meses sem valor.
/// </summary>
internal sealed class IbgeSidraIndexSource(HttpClient http) : IMonetaryIndexSource
{
    public string Name => "ibge";

    public bool Supports(MonetaryIndexKind kind) => kind is MonetaryIndexKind.IpcaE or MonetaryIndexKind.Inpc;

    public async Task<IReadOnlyList<IndexValue>> FetchAsync(MonetaryIndexKind kind, DateOnly firstMonth, DateOnly lastMonth, CancellationToken cancellationToken)
    {
        var (table, variable) = kind == MonetaryIndexKind.IpcaE ? ("3065", "355") : ("1736", "44");
        var url = string.Create(CultureInfo.InvariantCulture, $"values/t/{table}/n1/all/v/{variable}/p/{firstMonth:yyyyMM}-{lastMonth:yyyyMM}");
        await using var stream = await http.GetStreamAsync(new Uri(url, UriKind.Relative), cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return Parse(document.RootElement);
    }

    internal static IReadOnlyList<IndexValue> Parse(JsonElement rows)
    {
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
        {
            return [];
        }

        // A coluna do mês muda de posição conforme a consulta: acha pelo cabeçalho.
        var monthColumn = rows[0].EnumerateObject()
            .Where(c => c.Value.ValueKind == JsonValueKind.String && c.Value.GetString()!.StartsWith("Mês (Código)", StringComparison.Ordinal))
            .Select(c => c.Name)
            .FirstOrDefault()
            ?? throw new FormatException("Resposta do SIDRA sem a coluna do mês.");

        var values = new List<IndexValue>();
        foreach (var row in rows.EnumerateArray().Skip(1))
        {
            var value = row.GetProperty("V").GetString();
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent))
            {
                continue;
            }

            var month = DateOnly.ParseExact(row.GetProperty(monthColumn).GetString() + "01", "yyyyMMdd", CultureInfo.InvariantCulture);
            values.Add(new IndexValue(month, percent));
        }

        return values;
    }
}

/// <summary>
/// Banco Central, API SGS: SELIC acumulada no mês (série 4390) e TR do primeiro dia do mês (série 7811). Resposta
/// <c>[{"data":"01/09/2026","valor":"1.08"}]</c>.
/// </summary>
internal sealed class BcbSgsIndexSource(HttpClient http) : IMonetaryIndexSource
{
    public string Name => "bcb";

    public bool Supports(MonetaryIndexKind kind) => kind is MonetaryIndexKind.Selic or MonetaryIndexKind.Tr;

    public async Task<IReadOnlyList<IndexValue>> FetchAsync(MonetaryIndexKind kind, DateOnly firstMonth, DateOnly lastMonth, CancellationToken cancellationToken)
    {
        var series = kind == MonetaryIndexKind.Selic ? "4390" : "7811";
        var last = lastMonth.AddMonths(1).AddDays(-1);
        var url = string.Create(CultureInfo.InvariantCulture, $"dados/serie/bcdata.sgs.{series}/dados?formato=json&dataInicial={firstMonth:dd/MM/yyyy}&dataFinal={last:dd/MM/yyyy}");
        await using var stream = await http.GetStreamAsync(new Uri(url, UriKind.Relative), cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return Parse(document.RootElement);
    }

    internal static IReadOnlyList<IndexValue> Parse(JsonElement rows)
    {
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Resposta do SGS fora do formato esperado.");
        }

        return [.. rows.EnumerateArray().Select(row => new IndexValue(
            DateOnly.ParseExact(row.GetProperty("data").GetString()!, "dd/MM/yyyy", CultureInfo.InvariantCulture),
            decimal.Parse(row.GetProperty("valor").GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture)))];
    }
}

/// <summary>
/// Ipeadata (IPEA), reserva do BCB para a SELIC (<c>BM12_TJOVER12</c>) e a TR (<c>BM12_TJTR12</c>): republica as
/// séries do SGS. A API devolve a série inteira; o mês corrente pode vir parcial e é descartado pelo importador.
/// </summary>
internal sealed class IpeadataIndexSource(HttpClient http) : IMonetaryIndexSource
{
    public string Name => "ipeadata";

    public bool Supports(MonetaryIndexKind kind) => kind is MonetaryIndexKind.Selic or MonetaryIndexKind.Tr;

    public async Task<IReadOnlyList<IndexValue>> FetchAsync(MonetaryIndexKind kind, DateOnly firstMonth, DateOnly lastMonth, CancellationToken cancellationToken)
    {
        var series = kind == MonetaryIndexKind.Selic ? "BM12_TJOVER12" : "BM12_TJTR12";
        await using var stream = await http.GetStreamAsync(new Uri($"api/odata4/ValoresSerie(SERCODIGO='{series}')", UriKind.Relative), cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return [.. Parse(document.RootElement).Where(v => v.Month >= firstMonth && v.Month <= lastMonth)];
    }

    internal static IReadOnlyList<IndexValue> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("value", out var rows) || rows.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Resposta do Ipeadata fora do formato esperado.");
        }

        var values = new List<IndexValue>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.GetProperty("VALVALOR").ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var month = DateOnly.ParseExact(row.GetProperty("VALDATA").GetString()![..7] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            values.Add(new IndexValue(month, row.GetProperty("VALVALOR").GetDecimal()));
        }

        return values;
    }
}

/// <summary>Fonte falsa e determinística de dev e testes: todo mês tem valor, sem internet.</summary>
internal sealed class FakeIndexSource : IMonetaryIndexSource
{
    public string Name => "fake";

    public bool Supports(MonetaryIndexKind kind) => true;

    public Task<IReadOnlyList<IndexValue>> FetchAsync(MonetaryIndexKind kind, DateOnly firstMonth, DateOnly lastMonth, CancellationToken cancellationToken)
    {
        var values = new List<IndexValue>();
        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            values.Add(new IndexValue(month, PercentFor(kind, month)));
        }

        return Task.FromResult<IReadOnlyList<IndexValue>>(values);
    }

    internal static decimal PercentFor(MonetaryIndexKind kind, DateOnly month)
    {
        var baseline = kind switch
        {
            MonetaryIndexKind.IpcaE => 0.40m,
            MonetaryIndexKind.Inpc => 0.35m,
            MonetaryIndexKind.Selic => 0.90m,
            _ => 0.10m,
        };
        return baseline + ((month.Month % 3) * 0.01m);
    }
}

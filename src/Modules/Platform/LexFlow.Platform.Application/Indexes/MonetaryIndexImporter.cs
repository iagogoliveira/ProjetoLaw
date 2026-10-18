using LexFlow.Application.Abstractions;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Domain.Indexes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LexFlow.Platform.Application.Indexes;

/// <summary>A variação de um índice num mês, como veio da fonte.</summary>
public sealed record IndexValue(DateOnly Month, decimal Percent);

/// <summary>
/// Fonte oficial de um ou mais índices (IBGE, BCB, Ipeadata ou a falsa de dev e testes). Implementada na
/// Infrastructure; falha de rede ou resposta inesperada vira exceção, e o importador tenta a próxima fonte.
/// </summary>
public interface IMonetaryIndexSource
{
    /// <summary>Nome gravado na coluna <c>source</c> (ex.: <c>ibge</c>).</summary>
    string Name { get; }

    bool Supports(MonetaryIndexKind kind);

    /// <summary>Os meses publicados entre <paramref name="firstMonth"/> e <paramref name="lastMonth"/> (inclusive).</summary>
    Task<IReadOnlyList<IndexValue>> FetchAsync(MonetaryIndexKind kind, DateOnly firstMonth, DateOnly lastMonth, CancellationToken cancellationToken);
}

/// <summary>Quantos meses novos entraram de cada índice.</summary>
public sealed record IndexImportSummary(IReadOnlyDictionary<MonetaryIndexKind, int> Imported);

/// <summary>
/// Importa os meses que faltam de cada índice, do mês seguinte ao último da tabela até o anterior ao mês corrente (o
/// mês corrente ainda não fechou). Roda todo dia e é idempotente: enquanto a fonte não publica o mês, nada muda. As
/// fontes de cada índice são tentadas na ordem do registro; a seguinte só entra quando a anterior falha.
/// </summary>
public sealed partial class MonetaryIndexImporter(
    IPlatformDbContext db,
    IEnumerable<IMonetaryIndexSource> sources,
    IClock clock,
    ILogger<MonetaryIndexImporter> logger)
{
    /// <summary>Primeiro mês da tabela (o seed começa em janeiro de 2000).</summary>
    public static readonly DateOnly FirstMonth = new(2000, 1, 1);

    public async Task<IndexImportSummary> ImportAsync(CancellationToken cancellationToken)
    {
        var lastClosed = MonetaryIndex.FirstDayOf(clock.Today).AddMonths(-1);
        var imported = new Dictionary<MonetaryIndexKind, int>();
        foreach (var kind in Enum.GetValues<MonetaryIndexKind>())
        {
            var latest = await db.MonetaryIndexes.Where(i => i.Kind == kind).MaxAsync(i => (DateOnly?)i.Month, cancellationToken);
            var from = latest?.AddMonths(1) ?? FirstMonth;
            imported[kind] = from > lastClosed ? 0 : await ImportAsync(kind, from, lastClosed, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new IndexImportSummary(imported);
    }

    private async Task<int> ImportAsync(MonetaryIndexKind kind, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        foreach (var source in sources.Where(s => s.Supports(kind)))
        {
            IReadOnlyList<IndexValue> values;
            try
            {
                values = await source.FetchAsync(kind, from, to, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or FormatException or System.Text.Json.JsonException)
            {
                LogSourceFailed(logger, exception, source.Name, kind);
                continue;
            }

            var now = clock.UtcNow;
            var accepted = values
                .Select(v => v with { Month = MonetaryIndex.FirstDayOf(v.Month) })
                .Where(v => v.Month >= from && v.Month <= to)
                .DistinctBy(v => v.Month)
                .ToList();
            foreach (var value in accepted)
            {
                db.MonetaryIndexes.Add(new MonetaryIndex(kind, value.Month, value.Percent, source.Name, now));
            }

            LogImported(logger, accepted.Count, kind, source.Name);
            return accepted.Count;
        }

        LogNoSource(logger, kind);
        return 0;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fonte {Source} falhou ao importar {Index}; tentando a próxima.")]
    private static partial void LogSourceFailed(ILogger logger, Exception exception, string source, MonetaryIndexKind index);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} meses novos de {Index} importados de {Source}.")]
    private static partial void LogImported(ILogger logger, int count, MonetaryIndexKind index, string source);

    [LoggerMessage(Level = LogLevel.Error, Message = "Nenhuma fonte disponível para {Index}; a tabela segue sem os meses novos.")]
    private static partial void LogNoSource(ILogger logger, MonetaryIndexKind index);
}

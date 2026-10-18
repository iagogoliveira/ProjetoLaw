using LexFlow.Platform.Application.Indexes;

namespace LexFlow.Platform.Infrastructure.Indexes;

/// <summary>
/// Importação dos índices monetários (Hangfire). Roda todo dia e só grava os meses que faltam: o IBGE publica perto do
/// dia 10 do mês seguinte e o BCB logo no início, então a rodada diária pega cada mês no primeiro dia em que sai.
/// </summary>
public sealed class MonetaryIndexImportJob(MonetaryIndexImporter importer)
{
    public const string RecurringJobId = "platform-monetary-indexes-import";

    /// <summary>Todo dia às 06:00 (America/Sao_Paulo).</summary>
    public const string Cron = "0 6 * * *";

    public Task<IndexImportSummary> RunAsync() => importer.ImportAsync(CancellationToken.None);
}

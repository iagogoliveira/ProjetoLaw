using LexFlow.SharedKernel.Domain;

namespace LexFlow.Platform.Domain.Indexes;

/// <summary>Índices oficiais mantidos pela plataforma (RF-13).</summary>
public enum MonetaryIndexKind
{
    /// <summary>IPCA-E: a variação mensal do IPCA-15 (IBGE), a série que os tribunais usam como IPCA-E.</summary>
    IpcaE,

    /// <summary>INPC (IBGE).</summary>
    Inpc,

    /// <summary>Taxa SELIC acumulada no mês (BCB). Já embute correção e juros (STF, ADC 58).</summary>
    Selic,

    /// <summary>Taxa Referencial do primeiro dia do mês (BCB).</summary>
    Tr,
}

/// <summary>
/// Variação de um índice num mês, em % (ex.: 0,52 = 0,52%). Dado de referência, igual para todos os escritórios:
/// sem tenant e sem RLS, como os tribunais e os feriados. A chave é o índice e o mês (sempre o dia 1). Fica fora da
/// trilha de auditoria (que é por escritório): a origem e a data da importação ficam na própria linha.
/// </summary>
[NotAudited]
public sealed class MonetaryIndex
{
    public const int MaxSourceLength = 20;

    private MonetaryIndex()
    {
    }

    public MonetaryIndex(MonetaryIndexKind kind, DateOnly month, decimal percent, string source, DateTimeOffset importedAt)
    {
        Kind = kind;
        Month = FirstDayOf(month);
        Percent = percent;
        Source = source;
        ImportedAt = importedAt;
    }

    public MonetaryIndexKind Kind { get; private set; }

    /// <summary>Primeiro dia do mês de referência.</summary>
    public DateOnly Month { get; private set; }

    /// <summary>Variação no mês, em %.</summary>
    public decimal Percent { get; private set; }

    /// <summary>De onde veio o valor: <c>seed</c>, <c>ibge</c>, <c>bcb</c>, <c>ipeadata</c> ou <c>fake</c>.</summary>
    public string Source { get; private set; } = string.Empty;

    public DateTimeOffset ImportedAt { get; private set; }

    public static DateOnly FirstDayOf(DateOnly date) => new(date.Year, date.Month, 1);
}

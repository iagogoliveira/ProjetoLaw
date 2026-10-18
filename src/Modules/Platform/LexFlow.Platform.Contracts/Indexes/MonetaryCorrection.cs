using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Contracts.Indexes;

/// <summary>Índices oficiais da tabela da plataforma.</summary>
public enum MonetaryIndexCode
{
    /// <summary>IPCA-E (variação mensal do IPCA-15, IBGE).</summary>
    IpcaE,

    /// <summary>INPC (IBGE).</summary>
    Inpc,

    /// <summary>SELIC acumulada no mês (BCB); já inclui os juros.</summary>
    Selic,

    /// <summary>TR do primeiro dia do mês (BCB).</summary>
    Tr,
}

/// <summary>
/// Cálculo de correção monetária: valor, índice, início da correção, data do cálculo e, opcionalmente, juros simples
/// em % ao mês a partir de <see cref="InterestFrom"/>.
/// </summary>
public sealed record MonetaryCorrectionRequest(
    decimal Principal,
    MonetaryIndexCode Index,
    DateOnly CorrectFrom,
    DateOnly Until,
    DateOnly? InterestFrom,
    decimal? MonthlyInterestPercent);

/// <summary>Um mês da memória de cálculo.</summary>
public sealed record CorrectionMonthLine(DateOnly Month, decimal Percent, decimal AccumulatedFactor);

/// <summary>
/// Resultado com a memória de cálculo: fator acumulado, valor corrigido (nunca abaixo do original), meses e taxa de
/// juros aplicados, juros e total.
/// </summary>
public sealed record MonetaryCorrectionResult(
    decimal Principal,
    MonetaryIndexCode Index,
    decimal Factor,
    decimal CorrectedAmount,
    bool DeflationFloorApplied,
    decimal InterestMonths,
    decimal InterestPercent,
    decimal InterestAmount,
    decimal Total,
    IReadOnlyList<CorrectionMonthLine> Months);

/// <summary>Correção monetária com os índices da plataforma (RF-13). Implementado pelo Platform.</summary>
public interface IMonetaryCorrection
{
    Task<Result<MonetaryCorrectionResult>> CalculateAsync(MonetaryCorrectionRequest request, CancellationToken cancellationToken);

    /// <summary>Vários cálculos de uma vez (ex.: todos os valores dos pedidos de um processo), lendo a tabela uma vez por índice.</summary>
    Task<IReadOnlyList<Result<MonetaryCorrectionResult>>> CalculateManyAsync(IReadOnlyList<MonetaryCorrectionRequest> requests, CancellationToken cancellationToken);
}

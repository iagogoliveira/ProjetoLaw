using System.Globalization;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Domain.Indexes;

/// <summary>
/// Pedido de cálculo: o valor original, o índice, a data de início da correção, a data do cálculo e, se houver, os
/// juros simples em % ao mês a partir de uma data própria (ex.: a citação).
/// </summary>
public sealed record CorrectionInput(
    decimal Principal,
    MonetaryIndexKind Index,
    DateOnly CorrectFrom,
    DateOnly Until,
    DateOnly? InterestFrom = null,
    decimal? MonthlyInterestPercent = null);

/// <summary>Um mês da memória de cálculo: a variação do índice e o fator acumulado até ele.</summary>
public sealed record CorrectionMonth(DateOnly Month, decimal Percent, decimal AccumulatedFactor);

/// <summary>Resultado com a memória de cálculo. Valores em reais com 2 casas; o fator com 9.</summary>
public sealed record CorrectionOutcome(
    decimal Principal,
    decimal Factor,
    decimal CorrectedAmount,
    bool DeflationFloorApplied,
    decimal InterestMonths,
    decimal InterestPercent,
    decimal InterestAmount,
    decimal Total,
    IReadOnlyList<CorrectionMonth> Months);

/// <summary>
/// Correção monetária por índice mais juros simples (RF-13; decisões de 2026-10-18):
/// <list type="bullet">
/// <item>a correção aplica a variação de cada mês, do mês de início até o anterior ao mês do cálculo (um ano de
/// atraso = 12 variações), como a Calculadora do Cidadão do BCB;</item>
/// <item>IPCA-E, INPC e TR se acumulam de forma composta. A SELIC é a soma simples das taxas mensais, como a Receita
/// Federal e o Manual de Cálculos da Justiça Federal, e não admite juros extras (ela já os embute);</item>
/// <item>meses de deflação entram, mas o valor corrigido nunca fica abaixo do original (entendimento do STJ);</item>
/// <item>os juros simples incidem sobre o valor corrigido, pro rata dia: meses completos mais os dias restantes / 30;</item>
/// <item>mês sem índice publicado é erro, nunca estimativa.</item>
/// </list>
/// Arredondamento comercial (meio para cima) só nos valores em reais; o fator não é arredondado durante a conta.
/// </summary>
public static class MonetaryCorrectionCalculator
{
    /// <summary>Limite do período (100 anos).</summary>
    public const int MaxMonths = 1200;

    public const decimal MaxMonthlyInterestPercent = 100m;

    public static readonly Error ValueInvalid =
        Error.Validation("platform.correction_value_invalid", "O valor a corrigir não pode ser negativo.");

    public static readonly Error PeriodInvalid =
        Error.Validation("platform.correction_period_invalid", "A data do cálculo precisa ser igual ou posterior ao início da correção, num período de até 100 anos.");

    public static readonly Error InterestInvalid =
        Error.Validation("platform.correction_interest_invalid", "A taxa de juros vai de 0 a 100% ao mês.");

    public static readonly Error InterestStartRequired =
        Error.Validation("platform.correction_interest_start_required", "Informe a data de início dos juros.");

    public static readonly Error SelicWithInterest =
        Error.Validation("platform.correction_selic_with_interest", "A SELIC já inclui os juros: não informe juros com a SELIC.");

    public static Error IndexMissing(MonetaryIndexKind index, DateOnly month) => Error.BusinessRule(
        "platform.monetary_index_missing",
        $"A tabela da plataforma ainda não tem o {index} de {month.ToString("MM/yyyy", CultureInfo.InvariantCulture)}.");

    /// <summary>Os meses cujas variações entram na correção: do mês de <paramref name="from"/> até o anterior ao de <paramref name="until"/>.</summary>
    public static IReadOnlyList<DateOnly> MonthsBetween(DateOnly from, DateOnly until)
    {
        var months = new List<DateOnly>();
        for (var month = MonetaryIndex.FirstDayOf(from); month < MonetaryIndex.FirstDayOf(until); month = month.AddMonths(1))
        {
            months.Add(month);
        }

        return months;
    }

    /// <summary>Meses de juros pro rata dia: os meses completos mais os dias restantes divididos por 30.</summary>
    public static decimal InterestMonths(DateOnly from, DateOnly until)
    {
        if (until <= from)
        {
            return 0m;
        }

        var whole = ((until.Year - from.Year) * 12) + until.Month - from.Month;
        if (from.AddMonths(whole) > until)
        {
            whole--;
        }

        var days = until.DayNumber - from.AddMonths(whole).DayNumber;
        return whole + (days / 30m);
    }

    public static Result<CorrectionOutcome> Calculate(CorrectionInput input, IReadOnlyDictionary<DateOnly, decimal> percents)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(percents);

        var invalid = Validate(input);
        if (invalid is not null)
        {
            return invalid;
        }

        var months = new List<CorrectionMonth>();
        var factor = 1m;
        var selicSum = 0m;
        foreach (var month in MonthsBetween(input.CorrectFrom, input.Until))
        {
            if (!percents.TryGetValue(month, out var percent))
            {
                return IndexMissing(input.Index, month);
            }

            if (input.Index == MonetaryIndexKind.Selic)
            {
                selicSum += percent;
                factor = 1m + (selicSum / 100m);
            }
            else
            {
                factor *= 1m + (percent / 100m);
            }

            months.Add(new CorrectionMonth(month, percent, decimal.Round(factor, 9, MidpointRounding.AwayFromZero)));
        }

        var floorApplied = factor < 1m;
        var corrected = Money(input.Principal * (floorApplied ? 1m : factor));

        var rate = input.MonthlyInterestPercent ?? 0m;
        var interestMonths = rate > 0m ? InterestMonths(input.InterestFrom!.Value, input.Until) : 0m;
        var interestPercent = rate * interestMonths;
        var interest = Money(corrected * interestPercent / 100m);

        return new CorrectionOutcome(
            input.Principal,
            decimal.Round(factor, 9, MidpointRounding.AwayFromZero),
            corrected,
            floorApplied,
            decimal.Round(interestMonths, 6, MidpointRounding.AwayFromZero),
            decimal.Round(interestPercent, 6, MidpointRounding.AwayFromZero),
            interest,
            corrected + interest,
            months);
    }

    private static Error? Validate(CorrectionInput input)
    {
        if (input.Principal < 0m)
        {
            return ValueInvalid;
        }

        var span = ((input.Until.Year - input.CorrectFrom.Year) * 12) + input.Until.Month - input.CorrectFrom.Month;
        if (input.Until < input.CorrectFrom || span > MaxMonths)
        {
            return PeriodInvalid;
        }

        var rate = input.MonthlyInterestPercent ?? 0m;
        if (rate is < 0m or > MaxMonthlyInterestPercent)
        {
            return InterestInvalid;
        }

        if (rate > 0m && input.Index == MonetaryIndexKind.Selic)
        {
            return SelicWithInterest;
        }

        return rate > 0m && input.InterestFrom is null ? InterestStartRequired : null;
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

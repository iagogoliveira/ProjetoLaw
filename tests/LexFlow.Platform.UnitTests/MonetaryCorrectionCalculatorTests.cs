using LexFlow.Platform.Domain.Indexes;

namespace LexFlow.Platform.UnitTests;

/// <summary>Regras da correção com uma tabela pequena e inventada (os casos com índices reais ficam nos testes de integração).</summary>
public sealed class MonetaryCorrectionCalculatorTests
{
    private static readonly Dictionary<DateOnly, decimal> Table = new()
    {
        [new DateOnly(2025, 1, 1)] = 1.00m,
        [new DateOnly(2025, 2, 1)] = 2.00m,
        [new DateOnly(2025, 3, 1)] = -0.50m,
    };

    [Fact]
    public void The_months_go_from_the_start_month_to_the_one_before_the_calculation_month()
    {
        MonetaryCorrectionCalculator.MonthsBetween(new DateOnly(2025, 1, 31), new DateOnly(2025, 3, 1))
            .ShouldBe([new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1)]);
        MonetaryCorrectionCalculator.MonthsBetween(new DateOnly(2025, 1, 5), new DateOnly(2025, 1, 28)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("2025-01-15", "2025-03-30", "2.5")]
    [InlineData("2025-01-31", "2025-02-28", "1")]
    [InlineData("2025-01-31", "2025-04-15", "2.5")]
    [InlineData("2025-03-10", "2025-03-10", "0")]
    [InlineData("2025-05-10", "2025-03-10", "0")]
    public void Interest_counts_whole_months_plus_the_remaining_days_over_30(string from, string until, string months) =>
        MonetaryCorrectionCalculator.InterestMonths(DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(until, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(decimal.Parse(months, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Price_indexes_compound_and_selic_adds_up()
    {
        var compound = Calculate(new CorrectionInput(1000m, MonetaryIndexKind.Inpc, new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 1)));
        var selic = Calculate(new CorrectionInput(1000m, MonetaryIndexKind.Selic, new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 1)));

        compound.Factor.ShouldBe(1.0302m);
        compound.CorrectedAmount.ShouldBe(1030.20m);
        selic.Factor.ShouldBe(1.03m);
        selic.CorrectedAmount.ShouldBe(1030.00m);
        selic.Months.Select(m => m.AccumulatedFactor).ShouldBe([1.01m, 1.03m]);
    }

    [Fact]
    public void Interest_applies_on_the_corrected_value_pro_rata_and_rounds_half_up()
    {
        var outcome = Calculate(new CorrectionInput(1000m, MonetaryIndexKind.IpcaE, new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 16), new DateOnly(2025, 1, 1), 1m));

        outcome.CorrectedAmount.ShouldBe(1030.20m);
        outcome.InterestMonths.ShouldBe(2.5m);
        outcome.InterestPercent.ShouldBe(2.5m);
        outcome.InterestAmount.ShouldBe(25.76m); // 1030,20 × 2,5% = 25,755
        outcome.Total.ShouldBe(1055.96m);
    }

    [Fact]
    public void Deflation_counts_but_never_brings_the_value_below_the_original()
    {
        var deflation = Calculate(new CorrectionInput(1000m, MonetaryIndexKind.Tr, new DateOnly(2025, 3, 1), new DateOnly(2025, 4, 1)));
        var mixed = Calculate(new CorrectionInput(1000m, MonetaryIndexKind.Tr, new DateOnly(2025, 2, 1), new DateOnly(2025, 4, 1)));

        deflation.Factor.ShouldBe(0.995m);
        deflation.DeflationFloorApplied.ShouldBeTrue();
        deflation.CorrectedAmount.ShouldBe(1000m);
        mixed.Factor.ShouldBe(1.0149m);
        mixed.DeflationFloorApplied.ShouldBeFalse();
        mixed.CorrectedAmount.ShouldBe(1014.90m);
    }

    [Fact]
    public void A_month_without_index_is_an_error_never_an_estimate()
    {
        var result = MonetaryCorrectionCalculator.Calculate(new CorrectionInput(1000m, MonetaryIndexKind.Inpc, new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 1)), Table);

        result.Error!.Code.ShouldBe("platform.monetary_index_missing");
        result.Error.Message.ShouldContain("04/2025");
    }

    [Theory]
    [InlineData(-1, "2025-01-01", "2025-02-01", null, null, MonetaryIndexKind.Inpc, "platform.correction_value_invalid")]
    [InlineData(10, "2025-02-01", "2025-01-01", null, null, MonetaryIndexKind.Inpc, "platform.correction_period_invalid")]
    [InlineData(10, "1900-01-01", "2025-01-01", null, null, MonetaryIndexKind.Inpc, "platform.correction_period_invalid")]
    [InlineData(10, "2025-01-01", "2025-02-01", "2025-01-01", 101, MonetaryIndexKind.Inpc, "platform.correction_interest_invalid")]
    [InlineData(10, "2025-01-01", "2025-02-01", "2025-01-01", -1, MonetaryIndexKind.Inpc, "platform.correction_interest_invalid")]
    [InlineData(10, "2025-01-01", "2025-02-01", "2025-01-01", 1, MonetaryIndexKind.Selic, "platform.correction_selic_with_interest")]
    [InlineData(10, "2025-01-01", "2025-02-01", null, 1, MonetaryIndexKind.Inpc, "platform.correction_interest_start_required")]
    public void Invalid_requests_are_refused(int principal, string from, string until, string? interestFrom, int? rate, MonetaryIndexKind index, string code)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var input = new CorrectionInput(
            principal,
            index,
            DateOnly.Parse(from, culture),
            DateOnly.Parse(until, culture),
            interestFrom is null ? null : DateOnly.Parse(interestFrom, culture),
            rate);

        MonetaryCorrectionCalculator.Calculate(input, Table).Error!.Code.ShouldBe(code);
    }

    [Fact]
    public void Selic_with_zero_interest_is_accepted_and_the_month_starts_on_day_one()
    {
        Calculate(new CorrectionInput(10m, MonetaryIndexKind.Selic, new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1), null, 0m)).InterestAmount.ShouldBe(0m);
        new MonetaryIndex(MonetaryIndexKind.Tr, new DateOnly(2025, 1, 20), 0.1m, "seed", DateTimeOffset.UnixEpoch).Month.ShouldBe(new DateOnly(2025, 1, 1));
    }

    private static CorrectionOutcome Calculate(CorrectionInput input)
    {
        var result = MonetaryCorrectionCalculator.Calculate(input, Table);
        result.IsSuccess.ShouldBeTrue(result.Error?.Code);
        return result.Value;
    }
}

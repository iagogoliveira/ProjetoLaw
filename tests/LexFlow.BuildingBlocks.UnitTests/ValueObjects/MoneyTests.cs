using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.BuildingBlocks.UnitTests.ValueObjects;

public sealed class MoneyTests
{
    [Fact]
    public void Creates_with_normalized_currency_and_4_decimal_places()
    {
        var money = Money.Create(10.123456m, "brl").Value;

        money.Currency.ShouldBe("BRL");
        money.Amount.ShouldBe(10.1235m);
    }

    [Fact]
    public void Rounds_half_to_even()
    {
        Money.FromBrl(0.00005m).Amount.ShouldBe(0.0000m);
        Money.FromBrl(0.00015m).Amount.ShouldBe(0.0002m);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("R$")]
    [InlineData("REAL")]
    [InlineData("B1L")]
    public void Rejects_invalid_currency(string? currency) =>
        Money.Create(1m, currency).Error.ShouldBe(Money.InvalidCurrency);

    [Fact]
    public void Rejects_amounts_beyond_numeric_19_4() =>
        Money.Create(1_000_000_000_000_000m, Money.Brl).Error.ShouldBe(Money.OutOfRange);

    [Fact]
    public void Adds_subtracts_and_multiplies_same_currency()
    {
        var a = Money.FromBrl(100.10m);
        var b = Money.FromBrl(0.20m);

        (a + b).ShouldBe(Money.FromBrl(100.30m));
        (a - b).ShouldBe(Money.FromBrl(99.90m));
        (b * 3).ShouldBe(Money.FromBrl(0.60m));
        b.Negate().Amount.ShouldBe(-0.20m);
    }

    [Fact]
    public void Decimal_arithmetic_has_no_float_drift()
    {
        var total = Money.Zero();
        for (var i = 0; i < 10; i++)
        {
            total += Money.FromBrl(0.1m);
        }

        total.ShouldBe(Money.FromBrl(1m));
    }

    [Fact]
    public void Mixing_currencies_throws()
    {
        Should.Throw<InvalidOperationException>(() => Money.FromBrl(1m) + Money.Create(1m, "USD").Value);
    }

    [Fact]
    public void Formats_with_invariant_culture() => Money.FromBrl(1234.5m).ToString().ShouldBe("BRL 1234.50");
}

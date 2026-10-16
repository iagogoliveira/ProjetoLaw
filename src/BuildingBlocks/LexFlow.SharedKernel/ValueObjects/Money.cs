using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>
/// Valor monetário: <c>decimal</c> (nunca <c>float</c>) com 4 casas, como no banco (<c>numeric(19,4)</c>),
/// e moeda ISO 4217. Operações só entre a mesma moeda.
/// </summary>
public sealed record Money : ValueObject
{
    public const string Brl = "BRL";

    public static readonly Error InvalidCurrency = Error.Validation("money.invalid_currency", "Moeda inválida.");

    /// <summary>Limite de numeric(19,4).</summary>
    private const decimal MaxAbsolute = 999_999_999_999_999.9999m;

    public static readonly Error OutOfRange = Error.Validation("money.out_of_range", "Valor fora do intervalo permitido.");

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    /// <summary>Código ISO 4217 em maiúsculas (ex.: BRL).</summary>
    public string Currency { get; }

    public static Money Zero(string currency = Brl) => Create(0m, currency).Value;

    public static Money FromBrl(decimal amount) => Create(amount, Brl).Value;

    /// <summary>Arredonda para 4 casas (meio para o par, padrão bancário).</summary>
    public static Result<Money> Create(decimal amount, string? currency)
    {
        var code = (currency ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
        {
            return InvalidCurrency;
        }

        var rounded = decimal.Round(amount, 4, MidpointRounding.ToEven);
        return Math.Abs(rounded) > MaxAbsolute ? OutOfRange : new Money(rounded, code);
    }

    public static Money operator +(Money left, Money right) => Combine(left, right, left.Amount + right.Amount);

    public static Money operator -(Money left, Money right) => Combine(left, right, left.Amount - right.Amount);

    public static Money operator *(Money money, decimal factor)
    {
        ArgumentNullException.ThrowIfNull(money);
        return Create(money.Amount * factor, money.Currency).Value;
    }

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Multiply(Money money, decimal factor) => money * factor;

    public Money Negate() => new(-Amount, Currency);

    public override string ToString() => $"{Currency} {Amount.ToString("0.00##", System.Globalization.CultureInfo.InvariantCulture)}";

    private static Money Combine(Money left, Money right, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Não é possível operar {left.Currency} com {right.Currency}.");
        }

        return Create(amount, left.Currency).Value;
    }
}

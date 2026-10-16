using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>CPF com dígitos verificadores validados. Guardado só com dígitos.</summary>
public sealed record Cpf : ValueObject
{
    public static readonly Error Invalid = Error.Validation("cpf.invalid", "CPF inválido.");

    private static readonly int[] FirstWeights = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

    private Cpf(string value)
    {
        Value = value;
    }

    /// <summary>Somente os 11 dígitos.</summary>
    public string Value { get; }

    /// <summary>Formato 000.000.000-00.</summary>
    public string Formatted => $"{Value[..3]}.{Value[3..6]}.{Value[6..9]}-{Value[9..]}";

    public static bool IsValid(string? value) => Create(value).IsSuccess;

    public static Result<Cpf> Create(string? value)
    {
        var digits = DocumentDigits.Normalize(value);
        if (digits.Length != 11 || !digits.All(char.IsAsciiDigit) || DocumentDigits.AllSame(digits))
        {
            return Invalid;
        }

        var first = DocumentDigits.Mod11(digits, FirstWeights, c => c - '0');
        var second = DocumentDigits.Mod11(digits, SecondWeights, c => c - '0');

        return digits[9] - '0' == first && digits[10] - '0' == second ? new Cpf(digits) : Invalid;
    }

    public override string ToString() => Formatted;
}

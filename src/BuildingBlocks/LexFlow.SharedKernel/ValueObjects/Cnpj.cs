using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>
/// CNPJ numérico ou alfanumérico (IN RFB 2.229/2024): 12 posições de 0-9/A-Z e 2 dígitos verificadores
/// numéricos. Cada caractere vale o código ASCII menos 48, o que mantém o cálculo dos CNPJs numéricos.
/// </summary>
public sealed record Cnpj : ValueObject
{
    public static readonly Error Invalid = Error.Validation("cnpj.invalid", "CNPJ inválido.");

    private static readonly int[] FirstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private Cnpj(string value)
    {
        Value = value;
    }

    /// <summary>Os 14 caracteres sem máscara (letras em maiúsculas).</summary>
    public string Value { get; }

    /// <summary>Formato 00.000.000/0000-00.</summary>
    public string Formatted => $"{Value[..2]}.{Value[2..5]}.{Value[5..8]}/{Value[8..12]}-{Value[12..]}";

    public static bool IsValid(string? value) => Create(value).IsSuccess;

    public static Result<Cnpj> Create(string? value)
    {
        var normalized = DocumentDigits.Normalize(value);
        if (normalized.Length != 14
            || !normalized[..12].All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c))
            || !normalized[12..].All(char.IsAsciiDigit)
            || DocumentDigits.AllSame(normalized))
        {
            return Invalid;
        }

        var first = DocumentDigits.Mod11(normalized, FirstWeights, c => c - '0');
        var second = DocumentDigits.Mod11(normalized, SecondWeights, c => c - '0');

        return normalized[12] - '0' == first && normalized[13] - '0' == second ? new Cnpj(normalized) : Invalid;
    }

    public override string ToString() => Formatted;
}

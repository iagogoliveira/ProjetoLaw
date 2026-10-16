using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>CEP com 8 dígitos (a existência do logradouro não é verificada aqui).</summary>
public sealed record Cep : ValueObject
{
    public static readonly Error Invalid = Error.Validation("cep.invalid", "CEP inválido.");

    private Cep(string value)
    {
        Value = value;
    }

    /// <summary>Somente os 8 dígitos.</summary>
    public string Value { get; }

    /// <summary>Formato 00000-000.</summary>
    public string Formatted => $"{Value[..5]}-{Value[5..]}";

    public static bool IsValid(string? value) => Create(value).IsSuccess;

    public static Result<Cep> Create(string? value)
    {
        var digits = DocumentDigits.Normalize(value);
        return digits.Length == 8 && digits.All(char.IsAsciiDigit) && !string.Equals(digits, "00000000", StringComparison.Ordinal)
            ? new Cep(digits)
            : Invalid;
    }

    public override string ToString() => Formatted;
}

using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>
/// Telefone brasileiro: DDD (11–99, sem zero) + número de 8 dígitos (fixo, iniciando em 2–5)
/// ou 9 dígitos (celular, iniciando em 9). Aceita o prefixo 55 do país.
/// </summary>
public sealed record Phone : ValueObject
{
    public static readonly Error Invalid = Error.Validation("phone.invalid", "Telefone inválido.");

    private Phone(string areaCode, string number)
    {
        AreaCode = areaCode;
        Number = number;
    }

    /// <summary>DDD com 2 dígitos.</summary>
    public string AreaCode { get; }

    /// <summary>Número local com 8 ou 9 dígitos.</summary>
    public string Number { get; }

    public bool IsMobile => Number.Length == 9;

    /// <summary>Formato E.164 (ex.: +5511987654321).</summary>
    public string E164 => $"+55{AreaCode}{Number}";

    /// <summary>Formato (11) 98765-4321 ou (11) 3456-7890.</summary>
    public string Formatted => $"({AreaCode}) {Number[..^4]}-{Number[^4..]}";

    public static bool IsValid(string? value) => Create(value).IsSuccess;

    public static Result<Phone> Create(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        var digits = string.Concat(raw.Where(char.IsAsciiDigit));
        if (raw.Any(c => !char.IsAsciiDigit(c) && c is not (' ' or '(' or ')' or '-' or '+' or '.')))
        {
            return Invalid;
        }

        if (digits.Length is 12 or 13 && digits.StartsWith("55", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.Length is not (10 or 11) || digits[0] == '0' || digits[1] == '0')
        {
            return Invalid;
        }

        var number = digits[2..];
        var valid = number.Length == 9 ? number[0] == '9' : number[0] is >= '2' and <= '5';
        return valid ? new Phone(digits[..2], number) : Invalid;
    }

    public override string ToString() => Formatted;
}

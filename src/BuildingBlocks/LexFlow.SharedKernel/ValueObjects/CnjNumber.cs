using System.Text.RegularExpressions;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>
/// Numeração única de processos (Resolução CNJ 65/2008): <c>NNNNNNN-DD.AAAA.J.TR.OOOO</c>.
/// O dígito verificador segue o módulo 97 (ISO 7064): <c>NNNNNNN AAAA J TR OOOO DD</c> mod 97 = 1.
/// </summary>
public sealed partial record CnjNumber : ValueObject
{
    public static readonly Error Invalid = Error.Validation("cnj.invalid", "Número CNJ inválido.");

    private CnjNumber(string digits)
    {
        Digits = digits;
    }

    /// <summary>Os 20 dígitos na ordem do formato oficial (NNNNNNNDDAAAAJTROOOO).</summary>
    public string Digits { get; }

    /// <summary>Número sequencial do processo no ano, na unidade de origem.</summary>
    public string Sequential => Digits[..7];

    public string CheckDigits => Digits[7..9];

    public int Year => int.Parse(Digits[9..13], System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Segmento do Judiciário (J): 1 STF, 2 CNJ, 3 STJ, 4 Federal, 5 Trabalho, 6 Eleitoral, 7 Militar da União, 8 Estadual, 9 Militar Estadual.</summary>
    public int Segment => Digits[13] - '0';

    /// <summary>Tribunal (TR) dentro do segmento; 00 nos tribunais superiores e conselhos.</summary>
    public string Court => Digits[14..16];

    /// <summary>Unidade de origem (OOOO).</summary>
    public string Origin => Digits[16..];

    public string Formatted => $"{Sequential}-{CheckDigits}.{Digits[9..13]}.{Digits[13]}.{Court}.{Origin}";

    public static bool IsValid(string? value) => Create(value).IsSuccess;

    /// <summary>Aceita o formato oficial com máscara ou os 20 dígitos sem separadores.</summary>
    public static Result<CnjNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Invalid;
        }

        var trimmed = value.Trim();
        string digits;
        if (MaskedPattern().IsMatch(trimmed))
        {
            digits = string.Concat(trimmed.Where(char.IsAsciiDigit));
        }
        else if (trimmed.Length == 20 && trimmed.All(char.IsAsciiDigit))
        {
            digits = trimmed;
        }
        else
        {
            return Invalid;
        }

        if (digits[13] == '0' || !string.Equals(ComputeCheckDigits(digits), digits[7..9], StringComparison.Ordinal))
        {
            return Invalid;
        }

        return new CnjNumber(digits);
    }

    /// <summary>Calcula DD = 98 − (NNNNNNN AAAA J TR OOOO 00 mod 97), sem precisar de inteiros grandes.</summary>
    internal static string ComputeCheckDigits(string digits)
    {
        var withoutCheck = string.Concat(digits.AsSpan(0, 7), digits.AsSpan(9), "00");
        var remainder = 0;
        foreach (var c in withoutCheck)
        {
            remainder = ((remainder * 10) + (c - '0')) % 97;
        }

        return (98 - remainder).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }

    public override string ToString() => Formatted;

    [GeneratedRegex(@"^\d{7}-\d{2}\.\d{4}\.\d\.\d{2}\.\d{4}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MaskedPattern();
}

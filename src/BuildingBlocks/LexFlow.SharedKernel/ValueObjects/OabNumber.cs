using System.Text.RegularExpressions;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>Inscrição na OAB: número da seccional (só dígitos, sem zeros à esquerda) e UF.</summary>
public sealed partial record OabNumber : ValueObject
{
    public static readonly Error Invalid = Error.Validation("oab.invalid", "Número da OAB inválido.");

    private OabNumber(string number, string state)
    {
        Number = number;
        State = state;
    }

    public string Number { get; }

    /// <summary>UF da seccional.</summary>
    public string State { get; }

    /// <summary>Formato 123456/SP.</summary>
    public string Formatted => $"{Number}/{State}";

    public static Result<OabNumber> Create(string? number, string? state)
    {
        var digits = string.Concat((number ?? string.Empty).Where(c => c is not ('.' or ' ')));
        var uf = (state ?? string.Empty).Trim().ToUpperInvariant();

        if (digits.Length is < 1 or > 7 || !digits.All(char.IsAsciiDigit) || !BrazilianState.IsValid(uf))
        {
            return Invalid;
        }

        var normalized = digits.TrimStart('0');
        return normalized.Length == 0 ? Invalid : new OabNumber(normalized, uf);
    }

    /// <summary>Aceita "123456/SP", "123.456/SP", "SP 123456" e "OAB/SP 123456".</summary>
    public static Result<OabNumber> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Invalid;
        }

        var text = value.Trim().ToUpperInvariant();
        if (NumberThenState().Match(text) is { Success: true } a)
        {
            return Create(a.Groups["number"].Value, a.Groups["uf"].Value);
        }

        return StateThenNumber().Match(text) is { Success: true } b
            ? Create(b.Groups["number"].Value, b.Groups["uf"].Value)
            : Invalid;
    }

    public override string ToString() => Formatted;

    [GeneratedRegex(@"^(?<number>[\d.]+)\s*[/-]\s*(?<uf>[A-Z]{2})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NumberThenState();

    [GeneratedRegex(@"^(?:OAB\s*/?\s*)?(?<uf>[A-Z]{2})\s*(?<number>[\d.]+)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StateThenNumber();
}

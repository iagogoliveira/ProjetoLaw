using System.Text.RegularExpressions;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>Endereço de e-mail normalizado (sem espaços, em minúsculas), com até 254 caracteres.</summary>
public sealed partial record Email : ValueObject
{
    public static readonly Error Invalid = Error.Validation("email.invalid", "E-mail inválido.");

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool IsValid(string? value) => Create(value).IsSuccess;

    public static Result<Email> Create(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized.Length is > 0 and <= 254 && Pattern().IsMatch(normalized)
            ? new Email(normalized)
            : Invalid;
    }

    public override string ToString() => Value;

    // Parte local sem espaços nem @; domínio com ao menos um ponto e rótulos sem hífen nas pontas.
    [GeneratedRegex(@"^[^\s@]{1,64}@(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}

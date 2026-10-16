namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>Siglas das 27 unidades da federação.</summary>
public static class BrazilianState
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    };

    public static bool IsValid(string? uf) => uf is not null && All.Contains(uf);
}

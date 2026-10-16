namespace LexFlow.SharedKernel.ValueObjects;

/// <summary>Normalização e dígitos verificadores módulo 11 usados por CPF e CNPJ.</summary>
internal static class DocumentDigits
{
    /// <summary>Remove a máscara (pontos, traços, barras e espaços) e converte para maiúsculas.</summary>
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Where(c => c is not ('.' or '-' or '/' or ' '))).ToUpperInvariant();

    public static bool AllSame(string value) => value.Length > 0 && value.All(c => c == value[0]);

    /// <summary>Dígito módulo 11: resto menor que 2 vira 0; senão, 11 menos o resto.</summary>
    public static int Mod11(string body, int[] weights, Func<char, int> valueOf)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            sum += valueOf(body[i]) * weights[i];
        }

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}

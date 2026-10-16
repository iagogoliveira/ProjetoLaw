using System.Security.Cryptography;
using System.Text;
using LexFlow.Platform.Domain.Calendar;
using LexFlow.Platform.Domain.Courts;

namespace LexFlow.Platform.Infrastructure.Persistence;

/// <summary>
/// Seed versionado (seção 6.5) dos dados de referência da plataforma. Entra nas migrations via <c>HasData</c>;
/// alterações geram nova migration. IDs são determinísticos (derivados da chave natural) para o seed ser estável.
/// </summary>
internal static class ReferenceData
{
    private static readonly string[] StatesByCnjCode =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SE", "SP", "TO",
    ];

    /// <summary>Sede de cada TRT, na ordem das regiões (1ª a 24ª).</summary>
    private static readonly string[] LaborRegionSeats =
    [
        "RJ", "SP", "MG", "RS", "BA", "PE", "CE", "PA", "PR", "DF", "AM", "SC",
        "PB", "RO", "SP", "MA", "ES", "GO", "AL", "SE", "RN", "PI", "MT", "MS",
    ];

    private static readonly string[] FederalRegionSeats = ["DF", "RJ", "SP", "RS", "PE", "MG"];

    public static IReadOnlyList<Court> Courts { get; } = BuildCourts();

    public static IReadOnlyList<Holiday> NationalHolidays { get; } =
    [
        Fixed("Confraternização Universal", 1, 1, "Lei 662/1949", 1950),
        Fixed("Tiradentes", 4, 21, "Lei 662/1949 (redação da Lei 10.607/2002)", 1950),
        Fixed("Dia do Trabalho", 5, 1, "Lei 662/1949", 1950),
        Fixed("Independência do Brasil", 9, 7, "Lei 662/1949", 1950),
        Fixed("Nossa Senhora Aparecida", 10, 12, "Lei 6.802/1980", 1980),
        Fixed("Finados", 11, 2, "Lei 662/1949 (redação da Lei 10.607/2002)", 1950),
        Fixed("Proclamação da República", 11, 15, "Lei 662/1949", 1950),
        Fixed("Dia Nacional de Zumbi e da Consciência Negra", 11, 20, "Lei 14.759/2023", 2024),
        Fixed("Natal", 12, 25, "Lei 662/1949", 1950),
        new Holiday(IdFor("holiday:sexta-feira-santa"), "Sexta-feira Santa", HolidayRule.EasterRelative, null, null, -2, "Lei 9.093/1995", 1950),
    ];

    /// <summary>UUID determinístico (formato v8, RFC 9562) a partir de uma chave natural.</summary>
    public static Guid IdFor(string naturalKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("lexflow:" + naturalKey)).AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // versão 8 (personalizada)
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variante RFC
        return new Guid(bytes, bigEndian: true);
    }

    private static Holiday Fixed(string name, int month, int day, string legalBasis, int validFrom) =>
        new(IdFor($"holiday:{month:00}-{day:00}"), name, HolidayRule.FixedDate, month, day, null, legalBasis, validFrom);

    private static List<Court> BuildCourts()
    {
        var courts = new List<Court>
        {
            Court(1, 0, "STF", "Supremo Tribunal Federal", null),
            Court(2, 0, "CNJ", "Conselho Nacional de Justiça", null),
            Court(3, 0, "STJ", "Superior Tribunal de Justiça", null),
            Court(4, 90, "CJF", "Conselho da Justiça Federal", null),
            Court(5, 0, "TST", "Tribunal Superior do Trabalho", null),
            Court(5, 90, "CSJT", "Conselho Superior da Justiça do Trabalho", null),
            Court(6, 0, "TSE", "Tribunal Superior Eleitoral", null),
            Court(7, 0, "STM", "Superior Tribunal Militar", null),
            Court(9, 13, "TJMMG", "Tribunal de Justiça Militar de Minas Gerais", "MG"),
            Court(9, 21, "TJMRS", "Tribunal de Justiça Militar do Rio Grande do Sul", "RS"),
            Court(9, 26, "TJMSP", "Tribunal de Justiça Militar de São Paulo", "SP"),
        };

        for (var i = 0; i < FederalRegionSeats.Length; i++)
        {
            courts.Add(Court(4, i + 1, $"TRF{i + 1}", $"Tribunal Regional Federal da {i + 1}ª Região", FederalRegionSeats[i]));
        }

        for (var i = 0; i < LaborRegionSeats.Length; i++)
        {
            courts.Add(Court(5, i + 1, $"TRT{i + 1}", $"Tribunal Regional do Trabalho da {i + 1}ª Região", LaborRegionSeats[i]));
        }

        for (var i = 0; i < StatesByCnjCode.Length; i++)
        {
            var uf = StatesByCnjCode[i];
            courts.Add(Court(6, i + 1, $"TRE-{uf}", $"Tribunal Regional Eleitoral ({uf})", uf));
            var isFederalDistrict = string.Equals(uf, "DF", StringComparison.Ordinal);
            courts.Add(Court(
                8,
                i + 1,
                isFederalDistrict ? "TJDFT" : $"TJ{uf}",
                isFederalDistrict ? "Tribunal de Justiça do Distrito Federal e dos Territórios" : $"Tribunal de Justiça ({uf})",
                uf));
        }

        for (var region = 1; region <= 12; region++)
        {
            courts.Add(Court(7, region, $"{region}ª CJM", $"{region}ª Circunscrição Judiciária Militar", null));
        }

        return courts;
    }

    private static Court Court(int segment, int code, string acronym, string name, string? state) =>
        new(IdFor($"court:{segment}.{code:00}"), segment, code.ToString("00", System.Globalization.CultureInfo.InvariantCulture), acronym, name, state);
}

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

    /// <summary>Suspensão do CPC e dias sem expediente da Justiça Federal (Lei 5.010/1966, art. 62).</summary>
    public static IReadOnlyList<Holiday> ForensicCalendar { get; } =
    [
        Holiday.Of(IdFor("holiday:suspension:cpc-220"), "Suspensão dos prazos (recesso forense)", HolidayScope.National, null,
            HolidayKind.Suspension, HolidayRule.FixedDate, 12, 20, null, 32, "CPC, art. 220", 2016),
        Holiday.Of(IdFor("holiday:jf:recesso"), "Recesso da Justiça Federal", HolidayScope.FederalJustice, null,
            HolidayKind.Holiday, HolidayRule.FixedDate, 12, 20, null, 18, "Lei 5.010/1966, art. 62, I", 1967),
        Holiday.Of(IdFor("holiday:jf:semana-santa"), "Semana Santa (quarta-feira a domingo de Páscoa)", HolidayScope.FederalJustice, null,
            HolidayKind.Holiday, HolidayRule.EasterRelative, null, null, -4, 5, "Lei 5.010/1966, art. 62, II", 1967),
        Holiday.Of(IdFor("holiday:jf:carnaval"), "Segunda e terça-feira de Carnaval", HolidayScope.FederalJustice, null,
            HolidayKind.Holiday, HolidayRule.EasterRelative, null, null, -48, 2, "Lei 5.010/1966, art. 62, III", 1967),
        Holiday.Of(IdFor("holiday:jf:08-11"), "Dia da criação dos cursos jurídicos", HolidayScope.FederalJustice, null,
            HolidayKind.Holiday, HolidayRule.FixedDate, 8, 11, null, 1, "Lei 5.010/1966, art. 62, IV", 1967),
        Holiday.Of(IdFor("holiday:jf:11-01"), "Dia de Todos os Santos", HolidayScope.FederalJustice, null,
            HolidayKind.Holiday, HolidayRule.FixedDate, 11, 1, null, 1, "Lei 5.010/1966, art. 62, IV", 1967),
        Holiday.Of(IdFor("holiday:jf:12-08"), "Dia da Justiça", HolidayScope.FederalJustice, null,
            HolidayKind.Holiday, HolidayRule.FixedDate, 12, 8, null, 1, "Lei 5.010/1966, art. 62, IV", 1967),
    ];

    /// <summary>
    /// Feriados civis estaduais (Lei 9.093/1995, art. 1º, II, e leis estaduais), por UF. Entram só os confirmados em ato oficial
    /// recente; os de fonte fraca ficam de fora e são cadastrados pelo escritório (ver ADR 0037). Revisão jurídica pendente.
    /// </summary>
    public static IReadOnlyList<Holiday> StateHolidays { get; } =
    [
        StateFixed("AC", "Dia do Evangélico", 1, 23, "Lei estadual 1.538/2004", 2004),
        StateFixed("AC", "Dia Internacional da Mulher", 3, 8, "Lei estadual 1.411/2001", 2001),
        StateFixed("AC", "Aniversário do Acre", 6, 15, "Lei estadual 14/1964", 1964),
        StateFixed("AC", "Dia da Amazônia", 9, 5, "Lei estadual 1.526/2004", 2004),
        StateFixed("AL", "São João", 6, 24, "Lei estadual 5.508/1993", 1993),
        StateFixed("AL", "São Pedro", 6, 29, "Lei estadual 5.509/1993", 1993),
        StateFixed("AL", "Dia do Evangélico", 11, 30, "Lei estadual 7.530/2013", 2014),
        StateFixed("AM", "Elevação do Amazonas à categoria de Província", 9, 5, "Lei estadual 25/1977", 1977),
        StateFixed("AP", "Dia de São José", 3, 19, "Lei estadual 667/2002", 2002),
        StateFixed("AP", "Criação do Território Federal do Amapá", 9, 13, "Constituição do Estado do Amapá", 1991),
        StateFixed("BA", "Independência da Bahia", 7, 2, "Constituição do Estado da Bahia, art. 6º, § 3º", 1989),
        StateFixed("CE", "Data Magna do Ceará", 3, 25, "Constituição do Estado do Ceará, art. 18, parágrafo único", 1989),
        StateFixed("DF", "Dia do Evangélico", 11, 30, "Lei distrital 963/1995", 1995),
        StateEaster("ES", "Nossa Senhora da Penha", 8, "Lei estadual 11.010/2019", 2020),
        StateFixed("MA", "Adesão do Maranhão à Independência", 7, 28, "Lei estadual 2.457/1964", 1964),
        StateFixed("PA", "Adesão do Pará à Independência", 8, 15, "Lei estadual 5.999/1996", 1996),
        StateFixed("PB", "Fundação do Estado da Paraíba", 8, 5, "Lei estadual 3.489/1967", 1967),
        StateFixed("PE", "Data Magna de Pernambuco (Revolução de 1817)", 3, 6, "Lei estadual 16.241/2017", 2018),
        StateFixed("RJ", "Dia de São Jorge", 4, 23, "Lei estadual 5.198/2008", 2008),
        StateEaster("RJ", "Terça-feira de Carnaval", -47, "Lei estadual 5.243/2008", 2009),
        StateEaster("RJ", "Corpus Christi", 60, "Lei estadual 11.002/2025", 2026),
        StateFixed("RN", "Mártires de Cunhaú e Uruaçu", 10, 3, "Lei estadual 8.913/2006", 2007),
        StateFixed("RO", "Criação do Estado de Rondônia", 1, 4, "Lei estadual 2.291/2010", 2011),
        StateFixed("RO", "Dia do Evangélico", 6, 18, "Lei estadual 1.026/2001", 2002),
        StateFixed("RR", "Criação do Estado de Roraima", 10, 5, "Constituição do Estado de Roraima, art. 9º", 1991),
        StateFixed("RS", "Revolução Farroupilha", 9, 20, "Constituição do Estado do Rio Grande do Sul, art. 6º, parágrafo único", 1989),
        StateFixed("SE", "Emancipação Política de Sergipe", 7, 8, "Constituição do Estado de Sergipe, art. 269", 1989),
        StateFixed("SP", "Revolução Constitucionalista de 1932", 7, 9, "Lei estadual 9.497/1997", 1997),
        StateFixed("TO", "Senhor do Bonfim", 4, 15, "Lei estadual 4.509/2024", 2025),
        StateFixed("TO", "Nossa Senhora da Natividade", 9, 8, "Lei estadual 627/1993", 1994),
        StateFixed("TO", "Criação do Estado do Tocantins", 10, 5, "Lei estadual 98/1989", 1990),
    ];

    /// <summary>Todo o calendário da plataforma, na ordem em que entra no seed.</summary>
    public static IReadOnlyList<Holiday> Holidays { get; } = [.. NationalHolidays, .. ForensicCalendar, .. StateHolidays];

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

    private static Holiday StateFixed(string state, string name, int month, int day, string legalBasis, int validFrom) =>
        Holiday.Of(IdFor($"holiday:state:{state}:{month:00}-{day:00}"), name, HolidayScope.State, state, HolidayKind.Holiday,
            HolidayRule.FixedDate, month, day, null, 1, legalBasis, validFrom);

    private static Holiday StateEaster(string state, string name, int easterOffsetDays, string legalBasis, int validFrom) =>
        Holiday.Of(IdFor($"holiday:state:{state}:easter{easterOffsetDays:+0;-0}"), name, HolidayScope.State, state, HolidayKind.Holiday,
            HolidayRule.EasterRelative, null, null, easterOffsetDays, 1, legalBasis, validFrom);

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

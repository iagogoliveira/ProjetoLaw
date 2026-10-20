using LexFlow.Scheduling.Domain.Calendar;

namespace LexFlow.Scheduling.UnitTests.Fixtures;

/// <summary>
/// Calendário fixo dos testes (seção 13.3): feriados nacionais, a suspensão do CPC, alguns estaduais, a Justiça Federal e
/// três cadastros de escritório, de 2024 a 2028. Espelha as regras do seed da plataforma sem depender dele.
/// </summary>
internal static class TestCalendars
{
    public static readonly DateOnly From = new(2024, 1, 1);
    public static readonly DateOnly To = new(2028, 12, 31);

    /// <param name="sets">"SP", "RJ", "ES", "PE" e "JF", separados por "+" (vazio: só nacionais e recesso).</param>
    /// <param name="office">"MUN", "OUT", "OUT2" ou "SUS" (cadastros do escritório usados nos casos).</param>
    public static WorkingCalendar For(string sets, string? office = null)
    {
        var days = new List<NonWorkingDay>();
        for (var year = From.Year - 1; year <= To.Year; year++)
        {
            days.AddRange(National(year));
            days.AddRange(Range(new DateOnly(year, 12, 20), new DateOnly(year + 1, 1, 20), "Suspensão dos prazos (CPC, art. 220)", suspension: true));
            foreach (var set in sets.Split('+', StringSplitOptions.RemoveEmptyEntries))
            {
                days.AddRange(Extra(set, year));
            }
        }

        days.AddRange(Office(office));
        return new WorkingCalendar(From, To, days.Where(d => d.Date >= From && d.Date <= To));
    }

    public static DateOnly Easter(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var h = ((19 * a) + b - (b / 4) - ((b - ((b + 8) / 25) + 1) / 3) + 15) % 30;
        var l = (32 + (2 * (b % 4)) + (2 * (c / 4)) - h - (c % 4)) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        return new DateOnly(year, (h + l - (7 * m) + 114) / 31, ((h + l - (7 * m) + 114) % 31) + 1);
    }

    private static IEnumerable<NonWorkingDay> National(int year)
    {
        int[][] fixedDates = [[1, 1], [4, 21], [5, 1], [9, 7], [10, 12], [11, 2], [11, 15], [12, 25]];
        foreach (var d in fixedDates) { yield return Holiday(new DateOnly(year, d[0], d[1]), "Feriado nacional"); }
        if (year >= 2024) { yield return Holiday(new DateOnly(year, 11, 20), "Consciência Negra"); }
        yield return Holiday(Easter(year).AddDays(-2), "Sexta-feira Santa");
    }

    private static IEnumerable<NonWorkingDay> Extra(string set, int year) => set switch
    {
        "SP" => [Holiday(new DateOnly(year, 7, 9), "Revolução Constitucionalista")],
        "RJ" => year >= 2026
            ? [Holiday(new DateOnly(year, 4, 23), "São Jorge"), Holiday(Easter(year).AddDays(-47), "Carnaval (RJ)"), Holiday(Easter(year).AddDays(60), "Corpus Christi (RJ)")]
            : [Holiday(new DateOnly(year, 4, 23), "São Jorge"), Holiday(Easter(year).AddDays(-47), "Carnaval (RJ)")],
        "ES" => [Holiday(Easter(year).AddDays(8), "Nossa Senhora da Penha")],
        "PE" => [Holiday(new DateOnly(year, 3, 6), "Data Magna de Pernambuco")],
        "JF" =>
        [
            .. Range(new DateOnly(year, 12, 20), new DateOnly(year + 1, 1, 6), "Recesso da Justiça Federal", suspension: false),
            .. Range(Easter(year).AddDays(-4), Easter(year), "Semana Santa (JF)", suspension: false),
            .. Range(Easter(year).AddDays(-48), Easter(year).AddDays(-47), "Carnaval (JF)", suspension: false),
            Holiday(new DateOnly(year, 8, 11), "Cursos jurídicos"),
            Holiday(new DateOnly(year, 11, 1), "Todos os Santos"),
            Holiday(new DateOnly(year, 12, 8), "Dia da Justiça"),
        ],
        _ => throw new ArgumentException($"Conjunto desconhecido: {set}", nameof(set)),
    };

    private static IEnumerable<NonWorkingDay> Office(string? office) => office switch
    {
        null => [],
        "MUN" => [Holiday(new DateOnly(2026, 3, 19), "Aniversário do município")],
        "OUT" => [Holiday(new DateOnly(2026, 5, 15), "Indisponibilidade: sistema do tribunal")],
        "OUT2" => [Holiday(new DateOnly(2026, 3, 2), "Indisponibilidade: sistema do tribunal")],
        "SUS" => Range(new DateOnly(2026, 6, 8), new DateOnly(2026, 6, 12), "Suspensão determinada pelo tribunal", suspension: true),
        _ => throw new ArgumentException($"Cadastro desconhecido: {office}", nameof(office)),
    };

    private static NonWorkingDay Holiday(DateOnly date, string reason) => new(date, reason, IsSuspension: false);

    private static IEnumerable<NonWorkingDay> Range(DateOnly start, DateOnly end, string reason, bool suspension)
    {
        for (var day = start; day <= end; day = day.AddDays(1)) { yield return new NonWorkingDay(day, reason, suspension); }
    }
}

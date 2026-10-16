using LexFlow.SharedKernel.Domain;

namespace LexFlow.Platform.Domain.Calendar;

public enum HolidayRule
{
    /// <summary>Mesmo dia e mês todo ano.</summary>
    FixedDate,

    /// <summary>Deslocamento em dias a partir do domingo de Páscoa (ex.: Sexta-feira Santa = −2).</summary>
    EasterRelative,
}

public enum HolidayScope
{
    /// <summary>Feriado nacional por lei federal. Estaduais, municipais e forenses entram na Etapa 7.</summary>
    National,
}

/// <summary>
/// Regra de feriado mantida pela plataforma (dado de referência, sem tenant). As datas de cada ano são calculadas
/// por <see cref="HolidayCalendar"/>, então não é preciso cadastrar ano a ano.
/// </summary>
public sealed class Holiday : Entity<Guid>
{
    private Holiday()
    {
    }

    public Holiday(Guid id, string name, HolidayRule rule, int? month, int? day, int? easterOffsetDays, string legalBasis, int validFromYear, int? validToYear = null)
        : base(id)
    {
        Name = name;
        Rule = rule;
        Month = month;
        Day = day;
        EasterOffsetDays = easterOffsetDays;
        LegalBasis = legalBasis;
        ValidFromYear = validFromYear;
        ValidToYear = validToYear;
        Scope = HolidayScope.National;
    }

    public string Name { get; private set; } = string.Empty;

    public HolidayScope Scope { get; private set; }

    public HolidayRule Rule { get; private set; }

    public int? Month { get; private set; }

    public int? Day { get; private set; }

    public int? EasterOffsetDays { get; private set; }

    /// <summary>Fundamento legal (ex.: Lei 662/1949).</summary>
    public string LegalBasis { get; private set; } = string.Empty;

    public int ValidFromYear { get; private set; }

    public int? ValidToYear { get; private set; }

    public bool AppliesTo(int year) => year >= ValidFromYear && (ValidToYear is null || year <= ValidToYear);

    public DateOnly DateIn(int year) => Rule == HolidayRule.FixedDate
        ? new DateOnly(year, Month!.Value, Day!.Value)
        : HolidayCalendar.EasterSunday(year).AddDays(EasterOffsetDays!.Value);
}

/// <summary>Uma data de feriado já calculada.</summary>
public sealed record HolidayDate(DateOnly Date, string Name);

/// <summary>Calcula as datas dos feriados de um ano a partir das regras.</summary>
public static class HolidayCalendar
{
    public static IReadOnlyList<HolidayDate> For(int year, IEnumerable<Holiday> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return [.. rules.Where(r => r.AppliesTo(year)).Select(r => new HolidayDate(r.DateIn(year), r.Name)).OrderBy(h => h.Date)];
    }

    /// <summary>Domingo de Páscoa no calendário gregoriano (algoritmo anônimo de Meeus/Jones/Butcher).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }
}

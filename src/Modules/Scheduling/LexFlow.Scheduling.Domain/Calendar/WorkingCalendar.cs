namespace LexFlow.Scheduling.Domain.Calendar;

/// <summary>
/// Um dia em que o prazo não corre normalmente, com o motivo que aparece na memória de cálculo. <c>IsSuspension</c> marca a
/// suspensão do curso dos prazos (ex.: CPC, art. 220), que não conta nem em prazo de dias corridos; feriados e dias sem
/// expediente só deixam de contar em prazo de dias úteis, mas impedem o início e prorrogam o vencimento.
/// </summary>
public sealed record NonWorkingDay(DateOnly Date, string Reason, bool IsSuspension);

/// <summary>
/// Calendário de um processo num intervalo de datas: sábados e domingos mais os dias informados (plataforma e escritório).
/// Fora do intervalo coberto, a calculadora recusa a conta em vez de supor que o dia é útil.
/// </summary>
public sealed class WorkingCalendar
{
    public const string Saturday = "Sábado";
    public const string Sunday = "Domingo";

    private readonly Dictionary<DateOnly, NonWorkingDay> _days = [];

    public WorkingCalendar(DateOnly coversFrom, DateOnly coversTo, IEnumerable<NonWorkingDay> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (coversTo < coversFrom) { throw new ArgumentException("Intervalo do calendário inválido.", nameof(coversTo)); }
        CoversFrom = coversFrom;
        CoversTo = coversTo;
        foreach (var day in days.OrderBy(d => d.Date))
        {
            // Dois motivos no mesmo dia: a suspensão prevalece (é a regra mais restritiva) e os nomes se somam.
            if (_days.TryGetValue(day.Date, out var existing))
            {
                var reason = existing.Reason.Contains(day.Reason, StringComparison.Ordinal) ? existing.Reason : $"{existing.Reason}; {day.Reason}";
                _days[day.Date] = new NonWorkingDay(day.Date, reason, existing.IsSuspension || day.IsSuspension);
            }
            else
            {
                _days[day.Date] = day;
            }
        }
    }

    public DateOnly CoversFrom { get; }

    public DateOnly CoversTo { get; }

    public bool Covers(DateOnly date) => date >= CoversFrom && date <= CoversTo;

    /// <summary>Dia com expediente forense: nem fim de semana, nem feriado, nem suspensão.</summary>
    public bool IsBusinessDay(DateOnly date) => Reason(date) is null;

    public bool IsSuspended(DateOnly date) => _days.TryGetValue(date, out var day) && day.IsSuspension;

    /// <summary>Por que o dia não é útil, ou <c>null</c> se for. Feriados e suspensões aparecem antes do fim de semana.</summary>
    public string? Reason(DateOnly date)
    {
        if (_days.TryGetValue(date, out var day)) { return day.Reason; }
        return date.DayOfWeek switch
        {
            DayOfWeek.Saturday => Saturday,
            DayOfWeek.Sunday => Sunday,
            _ => null,
        };
    }
}

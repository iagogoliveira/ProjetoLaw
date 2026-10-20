using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain.Calendar;

/// <summary>Como o prazo começa (o fato que fixa o termo inicial).</summary>
public enum DeadlineStartKind
{
    /// <summary>Data do fato que inicia o prazo (CPC, art. 231: juntada do aviso ou do mandado, ciência, retirada dos autos etc.).</summary>
    EventDate,

    /// <summary>Data da publicação no diário (CPC, art. 224, §§ 2º e 3º).</summary>
    Publication,

    /// <summary>
    /// Data da disponibilização no Diário de Justiça Eletrônico Nacional (DJEN): considera-se publicada no primeiro dia útil
    /// seguinte (Lei 11.419/2006, art. 4º, § 3º).
    /// </summary>
    DjenAvailability,

    /// <summary>
    /// Envio ao portal eletrônico: intimação na data da consulta ou, sem consulta, dez dias corridos após o envio
    /// (Lei 11.419/2006, art. 5º, §§ 1º a 3º).
    /// </summary>
    PortalIntimation,
}

public enum CountingMode
{
    /// <summary>Só dias úteis (CPC, art. 219).</summary>
    BusinessDays,

    /// <summary>Dias corridos; só a suspensão interrompe a contagem, e o vencimento é prorrogado se cair em dia sem expediente.</summary>
    CalendarDays,
}

public enum CalculationStep
{
    /// <summary>O fato informado (disponibilização, publicação, envio ao portal ou fato do art. 231).</summary>
    Trigger,

    /// <summary>A publicação presumida no DJEN.</summary>
    Publication,

    /// <summary>A intimação pelo portal (consulta ou decurso dos dez dias).</summary>
    Intimation,

    /// <summary>Dia que não entra na contagem.</summary>
    Skipped,

    /// <summary>Dia contado; <see cref="CalculationLine.Count"/> traz a ordem.</summary>
    Counted,

    /// <summary>Dia sem expediente em que o vencimento cairia; o prazo é prorrogado para o dia útil seguinte (CPC, art. 224, § 1º).</summary>
    Extended,
}

/// <summary>
/// Dados do cálculo. <c>Days</c> é a quantidade de dias antes da dobra; <c>Doubled</c> dobra o prazo (Fazenda Pública,
/// Ministério Público, Defensoria, litisconsortes com procuradores distintos em autos físicos); <c>InternalLeadDays</c> diz quantos
/// dias úteis antes do vencimento fica a data interna (zero a deixa no próprio vencimento); <c>ConsultedOn</c> só vale para o portal.
/// </summary>
public sealed record DeadlineCalculationInput(DeadlineStartKind StartKind, DateOnly TriggerDate, DateOnly? ConsultedOn, int Days,
    CountingMode Mode, bool Doubled, int InternalLeadDays);

/// <summary>Uma linha da memória de cálculo.</summary>
public sealed record CalculationLine(DateOnly Date, CalculationStep Step, int? Count, string? Reason);

/// <summary>
/// Resultado: <c>IntimationOn</c> é o termo inicial (o dia seguinte é o primeiro candidato à contagem), <c>StartsOn</c> o primeiro
/// dia contado, <c>CountedDays</c> o total já dobrado e <c>Lines</c> a memória de cálculo.
/// </summary>
public sealed record DeadlineCalculation(DateOnly IntimationOn, DateOnly StartsOn, DateOnly FatalDueOn, DateOnly InternalDueOn,
    int CountedDays, IReadOnlyList<CalculationLine> Lines);

/// <summary>Calcula a sugestão de prazo. É puro e determinístico: não grava nada e não consulta relógio nem banco.</summary>
public interface IDeadlineCalculator
{
    Result<DeadlineCalculation> Calculate(DeadlineCalculationInput input, WorkingCalendar calendar);
}

public sealed class DeadlineCalculator : IDeadlineCalculator
{
    public const int MaxDays = 365;
    public const int MaxInternalLeadDays = 30;
    public const int PortalTacitDays = 10;
    private const string OutOfRange = "Data fora do calendário carregado.";

    public Result<DeadlineCalculation> Calculate(DeadlineCalculationInput input, WorkingCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(calendar);
        if (!IsValid(input)) { return SchedulingErrors.InvalidCalculation; }

        var memory = new Memory(calendar);
        try
        {
            var intimation = TermStart(input, memory);
            var total = input.Doubled ? input.Days * 2 : input.Days;
            var (startsOn, fatal) = input.Mode == CountingMode.BusinessDays
                ? CountBusinessDays(intimation, total, memory)
                : CountCalendarDays(intimation, total, memory);
            var internalDue = InternalDue(fatal, startsOn, input.InternalLeadDays, memory);
            return new DeadlineCalculation(intimation, startsOn, fatal, internalDue, total, memory.Lines);
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, OutOfRange, StringComparison.Ordinal))
        {
            return SchedulingErrors.CalendarOutOfRange;
        }
    }

    private static bool IsValid(DeadlineCalculationInput input) =>
        input.TriggerDate != default
        && input.Days is >= 1 and <= MaxDays
        && input.InternalLeadDays is >= 0 and <= MaxInternalLeadDays
        && Enum.IsDefined(input.StartKind) && Enum.IsDefined(input.Mode)
        && (input.ConsultedOn is null || (input.StartKind == DeadlineStartKind.PortalIntimation && input.ConsultedOn >= input.TriggerDate));

    /// <summary>O termo inicial, com as linhas do fato, da publicação presumida ou da intimação pelo portal.</summary>
    private static DateOnly TermStart(DeadlineCalculationInput input, Memory memory)
    {
        var trigger = input.TriggerDate;
        memory.Check(trigger);
        switch (input.StartKind)
        {
            case DeadlineStartKind.DjenAvailability:
                memory.Add(trigger, CalculationStep.Trigger, null, "Disponibilização no DJEN");
                var published = memory.NextBusinessDay(trigger);
                memory.Add(published, CalculationStep.Publication, null, "Publicação: primeiro dia útil após a disponibilização (Lei 11.419/2006, art. 4º, § 3º)");
                return published;
            case DeadlineStartKind.PortalIntimation:
                memory.Add(trigger, CalculationStep.Trigger, null, "Envio da intimação ao portal");
                return PortalIntimation(trigger, input.ConsultedOn, memory);
            case DeadlineStartKind.Publication:
                memory.Add(trigger, CalculationStep.Trigger, null, "Publicação no diário");
                return trigger;
            default:
                memory.Add(trigger, CalculationStep.Trigger, null, "Fato que inicia o prazo (CPC, art. 231)");
                return trigger;
        }
    }

    private static DateOnly PortalIntimation(DateOnly sentOn, DateOnly? consultedOn, Memory memory)
    {
        var tacit = sentOn.AddDays(PortalTacitDays);
        var consulted = consultedOn is { } c && c <= tacit;
        var date = consulted ? consultedOn!.Value : tacit;
        memory.Check(date);
        if (memory.Calendar.IsBusinessDay(date))
        {
            memory.Add(date, CalculationStep.Intimation, null, consulted
                ? "Intimação na data da consulta ao portal (Lei 11.419/2006, art. 5º, § 1º)"
                : "Intimação tácita: dez dias corridos após o envio (Lei 11.419/2006, art. 5º, § 3º)");
            return date;
        }

        memory.Add(date, CalculationStep.Skipped, null, $"{memory.Calendar.Reason(date)}: a intimação passa ao primeiro dia útil seguinte (Lei 11.419/2006, art. 5º, § 2º)");
        var next = memory.NextBusinessDay(date);
        memory.Add(next, CalculationStep.Intimation, null, consulted ? "Intimação pela consulta ao portal" : "Intimação tácita pelo portal");
        return next;
    }

    /// <summary>Dias úteis: exclui o dia do começo e conta só os dias com expediente (CPC, arts. 219 e 224).</summary>
    private static (DateOnly StartsOn, DateOnly Fatal) CountBusinessDays(DateOnly intimation, int total, Memory memory)
    {
        DateOnly? startsOn = null;
        var day = intimation;
        var count = 0;
        while (count < total)
        {
            day = memory.Advance(day);
            if (memory.Calendar.IsBusinessDay(day))
            {
                count++;
                startsOn ??= day;
                memory.Add(day, CalculationStep.Counted, count, null);
            }
            else
            {
                memory.Add(day, CalculationStep.Skipped, null, memory.Calendar.Reason(day));
            }
        }

        return (startsOn!.Value, day);
    }

    /// <summary>
    /// Dias corridos: o primeiro dia contado precisa ser útil (CPC, art. 224, § 1º); depois conta todos, exceto os de
    /// suspensão; o vencimento em dia sem expediente é prorrogado para o primeiro dia útil seguinte.
    /// </summary>
    private static (DateOnly StartsOn, DateOnly Fatal) CountCalendarDays(DateOnly intimation, int total, Memory memory)
    {
        var day = memory.Advance(intimation);
        while (!memory.Calendar.IsBusinessDay(day))
        {
            memory.Add(day, CalculationStep.Skipped, null, $"{memory.Calendar.Reason(day)}: o prazo só começa em dia útil");
            day = memory.Advance(day);
        }

        var startsOn = day;
        var count = 0;
        while (true)
        {
            if (memory.Calendar.IsSuspended(day))
            {
                memory.Add(day, CalculationStep.Skipped, null, memory.Calendar.Reason(day));
            }
            else
            {
                count++;
                memory.Add(day, CalculationStep.Counted, count, memory.Calendar.Reason(day));
                if (count == total) { break; }
            }

            day = memory.Advance(day);
        }

        if (!memory.Calendar.IsBusinessDay(day))
        {
            memory.MarkExtended(day);
            day = memory.NextBusinessDay(day);
            memory.Add(day, CalculationStep.Extended, null, "Vencimento prorrogado para o primeiro dia útil (CPC, art. 224, § 1º)");
        }

        return (startsOn, day);
    }

    /// <summary>A data interna recua dias úteis a partir do vencimento, sem passar do primeiro dia contado.</summary>
    private static DateOnly InternalDue(DateOnly fatal, DateOnly startsOn, int leadDays, Memory memory)
    {
        var day = fatal;
        for (var i = 0; i < leadDays; i++)
        {
            var previous = day.AddDays(-1);
            while (previous > startsOn && !memory.Calendar.IsBusinessDay(previous)) { previous = previous.AddDays(-1); }
            if (previous < startsOn) { break; }
            day = previous;
        }

        return day;
    }

    private sealed class Memory(WorkingCalendar calendar)
    {
        private readonly List<CalculationLine> _lines = [];

        public WorkingCalendar Calendar { get; } = calendar;

        public IReadOnlyList<CalculationLine> Lines => _lines;

        public void Add(DateOnly date, CalculationStep step, int? count, string? reason) => _lines.Add(new CalculationLine(date, step, count, reason));

        /// <summary>O vencimento cairia neste dia (a última linha contada): ela passa a explicar a prorrogação.</summary>
        public void MarkExtended(DateOnly date)
        {
            var index = _lines.Count - 1;
            _lines[index] = _lines[index] with { Reason = $"{Calendar.Reason(date)}: vencimento prorrogado" };
        }

        public void Check(DateOnly date)
        {
            if (!Calendar.Covers(date)) { throw new InvalidOperationException(OutOfRange); }
        }

        public DateOnly Advance(DateOnly date)
        {
            var next = date.AddDays(1);
            Check(next);
            return next;
        }

        /// <summary>O primeiro dia útil depois de <paramref name="date"/>, registrando os dias pulados.</summary>
        public DateOnly NextBusinessDay(DateOnly date)
        {
            var day = Advance(date);
            while (!Calendar.IsBusinessDay(day))
            {
                Add(day, CalculationStep.Skipped, null, Calendar.Reason(day));
                day = Advance(day);
            }

            return day;
        }
    }
}

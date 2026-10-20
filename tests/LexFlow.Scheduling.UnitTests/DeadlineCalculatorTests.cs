using System.Globalization;
using FsCheck;
using FsCheck.Fluent;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.Scheduling.UnitTests.Fixtures;

namespace LexFlow.Scheduling.UnitTests;

public sealed class DeadlineCalculatorTests
{
    private static readonly DeadlineCalculator Calculator = new();
    private static readonly string[] CalendarSets = ["", "SP", "RJ", "ES", "PE", "JF", "SP+JF"];

    [Theory]
    [MemberData(nameof(DeadlineCases.Data), MemberType = typeof(DeadlineCases))]
    public void Suite_case_matches_the_expected_dates(DeadlineCase c)
    {
        var result = Calculator.Calculate(new DeadlineCalculationInput(c.StartKind, Date(c.Trigger), c.ConsultedOn is null ? null : Date(c.ConsultedOn),
            c.Days, c.Mode, c.Doubled, c.InternalLeadDays), TestCalendars.For(c.Calendar, c.Office));

        result.IsSuccess.ShouldBeTrue();
        var value = result.Value;
        value.IntimationOn.ShouldBe(Date(c.IntimationOn));
        value.StartsOn.ShouldBe(Date(c.StartsOn));
        value.FatalDueOn.ShouldBe(Date(c.FatalDueOn));
        value.InternalDueOn.ShouldBe(Date(c.InternalDueOn));
    }

    [Fact]
    public void Suite_has_at_least_sixty_cases_with_unique_ids() =>
        DeadlineCases.All.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count().ShouldBeGreaterThanOrEqualTo(60);

    [Fact]
    public void Test_calendar_matches_known_easter_dates()
    {
        TestCalendars.Easter(2025).ShouldBe(new DateOnly(2025, 4, 20));
        TestCalendars.Easter(2026).ShouldBe(new DateOnly(2026, 4, 5));
        TestCalendars.Easter(2027).ShouldBe(new DateOnly(2027, 3, 28));
    }

    [Fact]
    public void Memory_lists_trigger_publication_skipped_and_counted_days_in_order()
    {
        // DJEN disponibilizado na sexta 06/03/2026: publicado na segunda 09/03, contagem a partir da terça 10/03.
        var value = Calculate(DeadlineStartKind.DjenAvailability, "2026-03-06", days: 3);

        value.Lines.Select(l => (l.Date, l.Step, l.Count)).ShouldBe(
        [
            (Date("2026-03-06"), CalculationStep.Trigger, null),
            (Date("2026-03-07"), CalculationStep.Skipped, null),
            (Date("2026-03-08"), CalculationStep.Skipped, null),
            (Date("2026-03-09"), CalculationStep.Publication, null),
            (Date("2026-03-10"), CalculationStep.Counted, 1),
            (Date("2026-03-11"), CalculationStep.Counted, 2),
            (Date("2026-03-12"), CalculationStep.Counted, 3),
        ]);
        value.Lines[1].Reason.ShouldBe(WorkingCalendar.Saturday);
        value.Lines[3].Reason!.ShouldContain("art. 4º, § 3º");
    }

    [Fact]
    public void Memory_names_the_holiday_that_was_skipped()
    {
        var value = Calculate(DeadlineStartKind.Publication, "2026-04-02", days: 1);

        value.Lines.ShouldContain(l => l.Date == Date("2026-04-03") && l.Step == CalculationStep.Skipped && l.Reason == "Sexta-feira Santa");
        value.FatalDueOn.ShouldBe(Date("2026-04-06"));
    }

    [Fact]
    public void Calendar_days_due_on_a_weekend_are_extended_and_explained()
    {
        // 5 dias corridos a partir de 03/03/2026: o 5º dia é sábado 07/03; prorroga para segunda 09/03.
        var value = Calculate(DeadlineStartKind.Publication, "2026-03-02", days: 5, mode: CountingMode.CalendarDays);

        value.Lines.Single(l => l.Count == 5).Reason!.ShouldContain("vencimento prorrogado");
        value.Lines[^1].ShouldBe(new CalculationLine(Date("2026-03-09"), CalculationStep.Extended, null,
            "Vencimento prorrogado para o primeiro dia útil (CPC, art. 224, § 1º)"));
    }

    [Fact]
    public void Portal_tacit_intimation_explains_the_ten_days()
    {
        var value = Calculate(DeadlineStartKind.PortalIntimation, "2026-03-02", days: 1);

        value.Lines.ShouldContain(l => l.Step == CalculationStep.Intimation && l.Date == Date("2026-03-12") && l.Reason!.Contains("tácita", StringComparison.Ordinal));
    }

    [Fact]
    public void Doubled_deadline_counts_twice_the_days()
    {
        var value = Calculate(DeadlineStartKind.Publication, "2026-03-02", days: 15, doubled: true);

        value.CountedDays.ShouldBe(30);
        value.Lines.Count(l => l.Step == CalculationStep.Counted).ShouldBe(30);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(366, 0)]
    [InlineData(15, -1)]
    [InlineData(15, 31)]
    public void Out_of_range_days_or_lead_are_rejected(int days, int lead)
    {
        var result = Calculator.Calculate(new DeadlineCalculationInput(DeadlineStartKind.Publication, Date("2026-03-02"), null, days,
            CountingMode.BusinessDays, false, lead), TestCalendars.For(""));

        result.Error.ShouldBe(SchedulingErrors.InvalidCalculation);
    }

    [Theory]
    [InlineData(DeadlineStartKind.Publication, "2026-03-05")]
    [InlineData(DeadlineStartKind.PortalIntimation, "2026-03-01")]
    public void Consultation_date_only_fits_a_portal_intimation_on_or_after_the_sending(DeadlineStartKind kind, string consulted)
    {
        var result = Calculator.Calculate(new DeadlineCalculationInput(kind, Date("2026-03-02"), Date(consulted), 5,
            CountingMode.BusinessDays, false, 0), TestCalendars.For(""));

        result.Error.ShouldBe(SchedulingErrors.InvalidCalculation);
    }

    [Fact]
    public void Calculation_past_the_loaded_calendar_is_refused_instead_of_guessing()
    {
        var calendar = new WorkingCalendar(Date("2026-03-01"), Date("2026-03-20"), []);

        var result = Calculator.Calculate(new DeadlineCalculationInput(DeadlineStartKind.Publication, Date("2026-03-02"), null, 15,
            CountingMode.BusinessDays, false, 0), calendar);

        result.Error.ShouldBe(SchedulingErrors.CalendarOutOfRange);
    }

    [Fact]
    public void Two_reasons_on_the_same_day_are_merged_and_suspension_prevails()
    {
        var day = Date("2026-12-25");
        var calendar = new WorkingCalendar(Date("2026-12-01"), Date("2026-12-31"),
            [new NonWorkingDay(day, "Natal", false), new NonWorkingDay(day, "Recesso", true), new NonWorkingDay(day, "Natal", false)]);

        calendar.IsSuspended(day).ShouldBeTrue();
        calendar.Reason(day).ShouldBe("Natal; Recesso");
    }

    [Fact]
    public void Property_fatal_date_is_a_business_day_after_the_term_and_counts_exactly_the_days() =>
        Prop.ForAll(Inputs(), sample =>
        {
            var (input, calendar) = sample;
            var result = Calculator.Calculate(input, calendar);
            if (result.IsFailure) { return false; }
            var v = result.Value;
            var total = input.Doubled ? input.Days * 2 : input.Days;
            return calendar.IsBusinessDay(v.FatalDueOn) && calendar.IsBusinessDay(v.StartsOn) && calendar.IsBusinessDay(v.InternalDueOn)
                && v.IntimationOn >= input.TriggerDate && v.StartsOn > v.IntimationOn && v.FatalDueOn >= v.StartsOn
                && v.InternalDueOn >= v.StartsOn && v.InternalDueOn <= v.FatalDueOn
                && v.CountedDays == total && v.Lines.Count(l => l.Step == CalculationStep.Counted) == total
                && v.Lines.Select(l => l.Date).SequenceEqual(v.Lines.Select(l => l.Date).Order())
                && (input.Mode == CountingMode.CalendarDays
                    || v.Lines.Where(l => l.Step == CalculationStep.Counted).All(l => calendar.IsBusinessDay(l.Date)));
        }).QuickCheckThrowOnFailure();

    [Fact]
    public void Property_more_days_or_doubling_never_moves_the_due_date_earlier() =>
        Prop.ForAll(Inputs(), sample =>
        {
            var (input, calendar) = sample;
            var fatal = Calculator.Calculate(input, calendar).Value.FatalDueOn;
            var longer = Calculator.Calculate(input with { Days = input.Days + 1 }, calendar).Value.FatalDueOn;
            var doubled = Calculator.Calculate(input with { Doubled = true }, calendar).Value.FatalDueOn;
            // Em dias corridos, N e N+1 podem cair no mesmo vencimento prorrogado (sábado e domingo vão para a segunda).
            return (input.Mode == CountingMode.BusinessDays ? longer > fatal : longer >= fatal) && doubled >= fatal;
        }).QuickCheckThrowOnFailure();

    [Fact]
    public void Property_calculation_is_deterministic() =>
        Prop.ForAll(Inputs(), sample =>
        {
            var (input, calendar) = sample;
            var first = Calculator.Calculate(input, calendar).Value;
            var second = Calculator.Calculate(input, calendar).Value;
            return first.FatalDueOn == second.FatalDueOn && first.Lines.SequenceEqual(second.Lines);
        }).QuickCheckThrowOnFailure();

    private static Arbitrary<(DeadlineCalculationInput Input, WorkingCalendar Calendar)> Inputs()
    {
        var gen =
            from offset in Gen.Choose(0, 1095)
            from days in Gen.Choose(1, 90)
            from lead in Gen.Choose(0, 10)
            from kind in Gen.Elements(Enum.GetValues<DeadlineStartKind>())
            from mode in Gen.Elements(Enum.GetValues<CountingMode>())
            from doubled in Gen.Elements(false, true)
            from consultedAfter in Gen.Choose(-1, 12)
            from set in Gen.Elements(CalendarSets)
            let trigger = TestCalendars.From.AddDays(offset)
            let consulted = kind == DeadlineStartKind.PortalIntimation && consultedAfter >= 0 ? trigger.AddDays(consultedAfter) : (DateOnly?)null
            select (new DeadlineCalculationInput(kind, trigger, consulted, days, mode, doubled, lead), TestCalendars.For(set));
        return gen.ToArbitrary();
    }

    private static DeadlineCalculation Calculate(DeadlineStartKind kind, string trigger, int days, CountingMode mode = CountingMode.BusinessDays,
        bool doubled = false) =>
        Calculator.Calculate(new DeadlineCalculationInput(kind, Date(trigger), null, days, mode, doubled, 0), TestCalendars.For("")).Value;

    private static DateOnly Date(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}

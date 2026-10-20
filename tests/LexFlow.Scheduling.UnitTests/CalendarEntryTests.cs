using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;

namespace LexFlow.Scheduling.UnitTests;

public sealed class CalendarEntryTests
{
    private static readonly DateOnly Day = new(2026, 10, 28);
    private static readonly Guid Court = Guid.CreateVersion7();

    private static CalendarEntryInput Input(CalendarEntryScope scope = CalendarEntryScope.Office, string? state = null, string? municipality = null,
        Guid? court = null, CalendarEntryKind kind = CalendarEntryKind.Holiday, DateOnly? start = null, DateOnly? end = null, bool recurs = false) =>
        new("Dia do servidor", kind, scope, state, municipality, court, start ?? Day, end ?? start ?? Day, recurs, null);

    [Fact]
    public void Office_scope_accepts_no_location_and_rejects_any_location()
    {
        CalendarEntry.Create(Input()).IsSuccess.ShouldBeTrue();
        CalendarEntry.Create(Input(state: "SP")).Error.ShouldBe(SchedulingErrors.InvalidCalendarEntry);
        CalendarEntry.Create(Input(municipality: "Santos")).Error.ShouldBe(SchedulingErrors.InvalidCalendarEntry);
        CalendarEntry.Create(Input(court: Court)).Error.ShouldBe(SchedulingErrors.InvalidCalendarEntry);
    }

    [Fact]
    public void State_scope_needs_a_valid_state_only()
    {
        var entry = CalendarEntry.Create(Input(CalendarEntryScope.State, state: " sp ")).Value;
        entry.StateCode.ShouldBe("SP");
        CalendarEntry.Create(Input(CalendarEntryScope.State)).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(CalendarEntryScope.State, state: "XX")).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(CalendarEntryScope.State, state: "SP", municipality: "Santos")).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(CalendarEntryScope.State, state: "SP", court: Court)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Municipality_scope_needs_state_and_municipality()
    {
        var entry = CalendarEntry.Create(Input(CalendarEntryScope.Municipality, "sp", "  São   Paulo ")).Value;
        entry.StateCode.ShouldBe("SP");
        entry.Municipality.ShouldBe("São Paulo");
        CalendarEntry.Create(Input(CalendarEntryScope.Municipality, "SP")).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(CalendarEntryScope.Municipality, municipality: "Santos")).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(CalendarEntryScope.Municipality, "SP", "Santos", Court)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Court_scope_needs_a_court_only()
    {
        CalendarEntry.Create(Input(CalendarEntryScope.Court, court: Court)).Value.CourtId.ShouldBe(Court);
        CalendarEntry.Create(Input(CalendarEntryScope.Court)).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(CalendarEntryScope.Court, court: Guid.Empty)).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(CalendarEntryScope.Court, "SP", court: Court)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Inverted_period_blank_name_and_long_recurring_span_are_refused()
    {
        CalendarEntry.Create(Input(start: Day, end: Day.AddDays(-1))).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input() with { Name = "  " }).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(start: Day, end: Day.AddDays(CalendarEntry.MaxRecurringSpanDays - 1), recurs: true)).IsSuccess.ShouldBeTrue();
        CalendarEntry.Create(Input(start: Day, end: Day.AddDays(CalendarEntry.MaxRecurringSpanDays), recurs: true)).IsFailure.ShouldBeTrue();
        CalendarEntry.Create(Input(start: Day, end: Day.AddDays(CalendarEntry.MaxSpanDays - 1))).IsSuccess.ShouldBeTrue();
        CalendarEntry.Create(Input(start: Day, end: Day.AddDays(CalendarEntry.MaxSpanDays))).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Update_clears_fields_that_no_longer_apply_to_the_scope()
    {
        var entry = CalendarEntry.Create(Input(CalendarEntryScope.Municipality, "SP", "Santos")).Value;
        entry.Update(Input()).IsSuccess.ShouldBeTrue();
        entry.StateCode.ShouldBeNull();
        entry.Municipality.ShouldBeNull();
        entry.Update(Input(CalendarEntryScope.State, state: "XX")).IsFailure.ShouldBeTrue();
        entry.Scope.ShouldBe(CalendarEntryScope.Office);
    }

    [Fact]
    public void Municipality_matching_ignores_accents_case_and_extra_spaces()
    {
        var entry = CalendarEntry.Create(Input(CalendarEntryScope.Municipality, "SP", "São  Paulo")).Value;
        entry.AppliesTo("SP", "SAO PAULO", null).ShouldBeTrue();
        entry.AppliesTo("sp", " sao   paulo ", null).ShouldBeTrue();
        entry.AppliesTo("RJ", "São Paulo", null).ShouldBeFalse();
        entry.AppliesTo("SP", "Santos", null).ShouldBeFalse();
        entry.AppliesTo("SP", null, null).ShouldBeFalse();
    }

    [Fact]
    public void Applies_to_follows_each_scope()
    {
        CalendarEntry.Create(Input()).Value.AppliesTo(null, null, null).ShouldBeTrue();
        var state = CalendarEntry.Create(Input(CalendarEntryScope.State, state: "SP")).Value;
        state.AppliesTo("sp", null, null).ShouldBeTrue();
        state.AppliesTo("RJ", null, null).ShouldBeFalse();
        var court = CalendarEntry.Create(Input(CalendarEntryScope.Court, court: Court)).Value;
        court.AppliesTo(null, null, Court).ShouldBeTrue();
        court.AppliesTo(null, null, Guid.CreateVersion7()).ShouldBeFalse();
        court.AppliesTo(null, null, null).ShouldBeFalse();
    }

    [Fact]
    public void Recurring_entry_crossing_the_new_year_repeats_in_later_years()
    {
        var entry = CalendarEntry.Create(Input(start: new(2025, 12, 30), end: new(2026, 1, 2), recurs: true)).Value;

        var days = entry.DaysBetween(new(2027, 12, 1), new(2028, 1, 31)).Select(d => d.Date).ToList();

        days.ShouldBe([new(2027, 12, 30), new(2027, 12, 31), new(2028, 1, 1), new(2028, 1, 2)], ignoreOrder: true);
    }

    [Fact]
    public void Recurring_entry_does_not_repeat_before_its_first_year()
    {
        var entry = CalendarEntry.Create(Input(start: new(2026, 3, 10), end: new(2026, 3, 11), recurs: true)).Value;

        entry.DaysBetween(new(2025, 1, 1), new(2025, 12, 31)).ShouldBeEmpty();
    }

    [Fact]
    public void Non_recurring_entry_returns_only_days_inside_the_range()
    {
        var entry = CalendarEntry.Create(Input(start: new(2026, 10, 28), end: new(2026, 10, 30))).Value;

        entry.DaysBetween(new(2026, 10, 29), new(2026, 11, 30)).Select(d => d.Date).ShouldBe([new(2026, 10, 29), new(2026, 10, 30)]);
        entry.DaysBetween(new(2027, 10, 1), new(2027, 12, 31)).ShouldBeEmpty();
        entry.DaysBetween(new(2026, 1, 1), new(2026, 10, 27)).ShouldBeEmpty();
    }

    [Fact]
    public void System_outage_has_prefixed_reason_and_is_not_a_suspension()
    {
        var day = CalendarEntry.Create(Input(kind: CalendarEntryKind.SystemOutage) with { Name = "PJe fora do ar" }).Value
            .DaysBetween(Day, Day).Single();

        day.Reason.ShouldBe("Indisponibilidade: PJe fora do ar");
        day.IsSuspension.ShouldBeFalse();
    }

    [Fact]
    public void Suspension_is_a_suspension_and_holiday_is_not()
    {
        var suspension = CalendarEntry.Create(Input(kind: CalendarEntryKind.Suspension)).Value.DaysBetween(Day, Day).Single();
        var holiday = CalendarEntry.Create(Input()).Value.DaysBetween(Day, Day).Single();

        suspension.IsSuspension.ShouldBeTrue();
        suspension.Reason.ShouldBe("Dia do servidor");
        holiday.IsSuspension.ShouldBeFalse();
    }
}

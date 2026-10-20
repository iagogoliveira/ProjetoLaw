using LexFlow.Platform.Domain.Calendar;

namespace LexFlow.Platform.UnitTests;

public sealed class HolidayCalendarTests
{
    [Theory]
    [InlineData(2000, 4, 23)]
    [InlineData(2019, 4, 21)]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2038, 4, 25)]
    public void Easter_sunday_matches_the_gregorian_calendar(int year, int month, int day) =>
        HolidayCalendar.EasterSunday(year).ShouldBe(new DateOnly(year, month, day));

    [Fact]
    public void Good_friday_is_two_days_before_easter()
    {
        var goodFriday = new Holiday(Guid.CreateVersion7(), "Sexta-feira Santa", HolidayRule.EasterRelative, null, null, -2, "Lei 9.093/1995", 1950);

        goodFriday.DateIn(2026).ShouldBe(new DateOnly(2026, 4, 3));
        goodFriday.DateIn(2026).DayOfWeek.ShouldBe(DayOfWeek.Friday);
    }

    [Fact]
    public void Rules_apply_only_within_their_validity()
    {
        var blackConsciousness = new Holiday(Guid.CreateVersion7(), "Consciência Negra", HolidayRule.FixedDate, 11, 20, null, "Lei 14.759/2023", 2024);

        blackConsciousness.AppliesTo(2023).ShouldBeFalse();
        blackConsciousness.AppliesTo(2024).ShouldBeTrue();
    }

    [Fact]
    public void Calendar_lists_dates_of_the_year_in_order()
    {
        Holiday[] rules =
        [
            new(Guid.CreateVersion7(), "Natal", HolidayRule.FixedDate, 12, 25, null, "Lei 662/1949", 1950),
            new(Guid.CreateVersion7(), "Confraternização Universal", HolidayRule.FixedDate, 1, 1, null, "Lei 662/1949", 1950),
            new(Guid.CreateVersion7(), "Sexta-feira Santa", HolidayRule.EasterRelative, null, null, -2, "Lei 9.093/1995", 1950),
            new(Guid.CreateVersion7(), "Consciência Negra", HolidayRule.FixedDate, 11, 20, null, "Lei 14.759/2023", 2024),
        ];

        var dates = HolidayCalendar.For(2023, rules);

        dates.Select(d => d.Date).ShouldBe([new DateOnly(2023, 1, 1), new DateOnly(2023, 4, 7), new DateOnly(2023, 12, 25)]);
    }

    [Fact]
    public void A_span_covers_consecutive_days_into_the_next_year()
    {
        var recess = Holiday.Of(Guid.CreateVersion7(), "Recesso", HolidayScope.National, null, HolidayKind.Suspension, HolidayRule.FixedDate,
            12, 20, null, 32, "CPC, art. 220", 2016);

        var dates = recess.DatesIn(2026).ToList();

        dates.Count.ShouldBe(32);
        dates[0].ShouldBe(new DateOnly(2026, 12, 20));
        dates[^1].ShouldBe(new DateOnly(2027, 1, 20));
        HolidayCalendar.For(2026, [recess]).Count.ShouldBe(32);
    }

    [Fact]
    public void Holy_week_of_the_federal_justice_runs_from_wednesday_to_easter_sunday()
    {
        var holyWeek = Holiday.Of(Guid.CreateVersion7(), "Semana Santa", HolidayScope.FederalJustice, null, HolidayKind.Holiday,
            HolidayRule.EasterRelative, null, null, -4, 5, "Lei 5.010/1966, art. 62, II", 1967);

        holyWeek.DatesIn(2026).ShouldBe([new(2026, 4, 1), new(2026, 4, 2), new(2026, 4, 3), new(2026, 4, 4), new(2026, 4, 5)]);
    }

    [Theory]
    [InlineData(HolidayScope.State, null, 1)]
    [InlineData(HolidayScope.National, "SP", 1)]
    [InlineData(HolidayScope.State, "SP", 0)]
    public void Scope_requires_a_state_only_when_it_is_a_state_holiday(HolidayScope scope, string? state, int span) =>
        Should.Throw<ArgumentException>(() => Holiday.Of(Guid.CreateVersion7(), "X", scope, state, HolidayKind.Holiday,
            HolidayRule.FixedDate, 1, 2, null, span, "Lei", 2000));
}

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
}

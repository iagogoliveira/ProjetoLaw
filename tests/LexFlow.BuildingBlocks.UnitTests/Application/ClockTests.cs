using LexFlow.Application.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace LexFlow.BuildingBlocks.UnitTests.Application;

/// <summary>FakeTimeProvider + TimeProviderClock = o "FakeClock" da seção 13.3.</summary>
public sealed class ClockTests
{
    [Fact]
    public void Utc_now_comes_from_the_time_provider()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        var clock = new TimeProviderClock(time);

        time.Advance(TimeSpan.FromMinutes(5));

        clock.UtcNow.ShouldBe(new DateTimeOffset(2026, 10, 16, 12, 5, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Today_uses_sao_paulo_time_zone()
    {
        // 02:00 UTC de 17/10 ainda é 16/10 em São Paulo (UTC−3).
        var clock = new TimeProviderClock(new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 2, 0, 0, TimeSpan.Zero)));

        clock.Today.ShouldBe(new DateOnly(2026, 10, 16));
        clock.LegalTimeZone.Id.ShouldBe("America/Sao_Paulo");
    }

    [Fact]
    public void Ids_are_version_7_and_ordered_by_clock()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        var ids = new GuidV7IdGenerator(new TimeProviderClock(time));

        var first = ids.NewId();
        time.Advance(TimeSpan.FromMilliseconds(2));
        var second = ids.NewId();

        first.Version.ShouldBe(7);
        string.CompareOrdinal(first.ToString(), second.ToString()).ShouldBeLessThan(0);
    }
}

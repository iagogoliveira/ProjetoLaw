using LexFlow.Application.Abstractions;
using LexFlow.Platform.Application.Control;
using LexFlow.Platform.Contracts.Control;

namespace LexFlow.Platform.UnitTests;

public sealed class ControlReportTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Report_uses_office_civil_day_and_preserves_viewer_and_source_totals()
    {
        var source = new RecordingSource();
        var userId = Guid.CreateVersion7();
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 21, 1, 0, 0, TimeSpan.Zero));
        var handler = new GetControlReportHandler([source], clock);

        var result = await handler.Handle(new GetControlReportQuery(userId, false, null, null), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AsOf.ShouldBe(clock.UtcNow);
        result.Value.From.ShouldBe(new DateOnly(2026, 9, 20));
        result.Value.Until.ShouldBe(new DateOnly(2026, 10, 21));
        result.Value.Sections.ShouldHaveSingleItem().Count.ShouldBe(12);
        var window = source.Window.ShouldNotBeNull();
        window.UserId.ShouldBe(userId);
        window.MultiFactorAuthenticated.ShouldBeFalse();
        window.From.ShouldBe(new DateTimeOffset(2026, 9, 20, 3, 0, 0, TimeSpan.Zero));
        window.Until.ShouldBe(new DateTimeOffset(2026, 10, 21, 3, 0, 0, TimeSpan.Zero));
        window.IdleDays.ShouldBe(30);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(3651, 50)]
    [InlineData(30, 0)]
    [InlineData(30, 101)]
    public async Task Invalid_limits_do_not_invoke_sources(int idleDays, int limit)
    {
        var source = new RecordingSource();
        var handler = new GetControlReportHandler([source], new FixedClock(new DateTimeOffset(2026, 10, 21, 15, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(new GetControlReportQuery(Guid.CreateVersion7(), true, null, null, idleDays, limit), Ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("control.invalid_query");
        source.Window.ShouldBeNull();
    }

    [Theory]
    [InlineData(2026, 10, 21)]
    [InlineData(2026, 10, 20)]
    [InlineData(2027, 10, 23)]
    public async Task Reversed_empty_or_excessive_period_is_rejected(int year, int month, int day)
    {
        var source = new RecordingSource();
        var handler = new GetControlReportHandler([source], new FixedClock(new DateTimeOffset(2026, 10, 21, 15, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(new GetControlReportQuery(Guid.CreateVersion7(), true,
            new DateOnly(2026, 10, 21), new DateOnly(year, month, day)), Ct);

        result.IsFailure.ShouldBeTrue();
        source.Window.ShouldBeNull();
    }

    private sealed record FixedClock(DateTimeOffset UtcNow) : IClock
    {
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, LegalTimeZone).DateTime);
    }

    private sealed class RecordingSource : IControlReportSource
    {
        public ControlWindow? Window { get; private set; }

        public Task<ControlSourceResult> ReadAsync(ControlWindow window, CancellationToken cancellationToken)
        {
            Window = window;
            return Task.FromResult(new ControlSourceResult([new ControlSection("staleCases", 12, [])], []));
        }
    }
}

using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Domain;

namespace LexFlow.Publications.UnitTests;

public sealed class CaptureScheduleTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    private static readonly DateOnly Day = new(2026, 10, 19);
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 10, 19, hour, minute, 0, TimeSpan.FromHours(-3));
    private static CaptureSource Source(DateTimeOffset activation)
    {
        var source = CaptureSource.Create(new(CaptureSourceKind.Name, "Fonte", "Maria")).Value;
        source.ActivateAt(activation);
        return source;
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(8, false)]
    public void Morning_capture_starts_at_five_and_retries_stop_at_seven(int hour, bool expected)
    {
        var source = Source(At(0));
        CaptureSchedule.CanCapture(source, Day, At(hour), Zone, false).ShouldBe(expected);
        CaptureSchedule.Deadline(source, Day, Zone).ShouldBe(At(7));
    }

    [Fact]
    public void A_late_activation_gets_one_hour_and_does_not_run_before_activation()
    {
        var source = Source(At(8, 15));
        CaptureSchedule.CanCapture(source, Day, At(8), Zone, false).ShouldBeFalse();
        CaptureSchedule.CanCapture(source, Day, At(8, 15), Zone, false).ShouldBeTrue();
        CaptureSchedule.CanCapture(source, Day, At(9, 14), Zone, false).ShouldBeTrue();
        CaptureSchedule.CanCapture(source, Day, At(9, 15), Zone, false).ShouldBeFalse();
        CaptureSchedule.CanCapture(source, Day, At(12), Zone, true).ShouldBeTrue();
        CaptureSchedule.Deadline(source, Day, Zone).ShouldBe(At(9, 15));
    }

    [Fact]
    public void The_late_activation_grace_period_survives_midnight()
    {
        var activation = At(23, 30);
        var source = Source(activation);
        CaptureSchedule.CanCapture(source, Day, activation.AddMinutes(45), Zone, false).ShouldBeTrue();
        CaptureSchedule.CanCapture(source, Day, activation.AddHours(1), Zone, false).ShouldBeFalse();
        CaptureSchedule.CanCapture(source, Day.AddDays(1), activation.AddMinutes(45), Zone, false).ShouldBeFalse();
        CaptureSchedule.Deadline(source, Day, Zone).ShouldBe(activation.AddHours(1));
    }

    [Fact]
    public void An_inactive_or_future_source_is_never_captured()
    {
        var source = Source(At(8));
        source.Update(new(CaptureSourceKind.Name, "Fonte", "Maria", false));
        CaptureSchedule.CanCapture(source, Day, At(9), Zone, true).ShouldBeFalse();
        source.Update(new(CaptureSourceKind.Name, "Fonte", "Maria", true));
        CaptureSchedule.CanCapture(source, Day.AddDays(1), At(9), Zone, true).ShouldBeFalse();
        CaptureSchedule.CanCapture(source, Day.AddDays(-1), At(9), Zone, true).ShouldBeFalse();
    }

    [Fact]
    public void An_alert_is_resolved_once_and_reopened_without_creating_another_row()
    {
        var alert = CaptureAlert.Raise(Guid.CreateVersion7(), Day, At(7), "publications.capture_missing");
        var id = alert.Id;
        alert.Reopen(At(7, 5), "publications.capture_missing").ShouldBeFalse();
        alert.Resolve(At(8));
        alert.Resolve(At(9));
        alert.ResolvedAt.ShouldBe(At(8));
        alert.Reopen(At(10), "publications.provider_unavailable").ShouldBeTrue();
        alert.Id.ShouldBe(id);
        alert.ResolvedAt.ShouldBeNull();
        alert.ErrorCode.ShouldBe("publications.provider_unavailable");
    }
}

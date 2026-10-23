using LexFlow.Scheduling.Domain.CalendarSync;

namespace LexFlow.Scheduling.UnitTests;

public sealed class CalendarSyncDomainTests
{
    private static readonly Guid UserId = Guid.Parse("019a0000-0000-7000-8000-000000000011");
    private static readonly DateOnly From = new(2026, 1, 1);

    [Theory]
    [InlineData((CalendarProvider)99)]
    public void Connect_rejects_invalid_provider(CalendarProvider provider)
    {
        CalendarConnection.Connect(UserId, provider, From, From.AddDays(1)).Error!.Code
            .ShouldBe(CalendarSyncErrors.InvalidConnection.Code);
    }

    [Fact]
    public void Connect_rejects_empty_user()
    {
        CalendarConnection.Connect(Guid.Empty, CalendarProvider.Google, From, From.AddDays(1)).Error!.Code
            .ShouldBe(CalendarSyncErrors.InvalidConnection.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(367)]
    public void Connect_rejects_invalid_window_lengths(int days)
    {
        CalendarConnection.Connect(UserId, CalendarProvider.Google, From, From.AddDays(days)).Error!.Code
            .ShouldBe(CalendarSyncErrors.InvalidConnection.Code);
    }

    [Fact]
    public void Connect_rejects_default_dates()
    {
        CalendarConnection.Connect(UserId, CalendarProvider.Google, default, From.AddDays(1)).Error!.Code
            .ShouldBe(CalendarSyncErrors.InvalidConnection.Code);
        CalendarConnection.Connect(UserId, CalendarProvider.Google, From, default).Error!.Code
            .ShouldBe(CalendarSyncErrors.InvalidConnection.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(366)]
    public void Connect_accepts_window_limits(int days)
    {
        var result = CalendarConnection.Connect(UserId, CalendarProvider.Microsoft365, From, From.AddDays(days));

        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(UserId);
        result.Value.Provider.ShouldBe(CalendarProvider.Microsoft365);
        result.Value.From.ShouldBe(From);
        result.Value.Until.ShouldBe(From.AddDays(days));
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Activate_validates_window_and_reactivates_without_changing_identity()
    {
        var connection = Connected();
        var originalId = connection.Id;
        connection.Disconnect();

        connection.Activate(default, From.AddDays(10)).Error!.Code.ShouldBe(CalendarSyncErrors.InvalidConnection.Code);
        connection.Activate(From, From.AddDays(367)).Error!.Code.ShouldBe(CalendarSyncErrors.InvalidConnection.Code);

        var newFrom = From.AddDays(5);
        var newUntil = From.AddDays(20);
        connection.Activate(newFrom, newUntil).IsSuccess.ShouldBeTrue();
        connection.IsActive.ShouldBeTrue();
        connection.Id.ShouldBe(originalId);
        connection.UserId.ShouldBe(UserId);
        connection.Provider.ShouldBe(CalendarProvider.Google);
        connection.From.ShouldBe(newFrom);
        connection.Until.ShouldBe(newUntil);
    }

    [Fact]
    public void Disconnect_preserves_last_success_timestamp()
    {
        var connection = Connected();
        var syncedAt = new DateTimeOffset(2026, 10, 22, 15, 30, 0, TimeSpan.Zero);
        connection.Succeeded(syncedAt);

        connection.Disconnect();

        connection.IsActive.ShouldBeFalse();
        connection.LastSyncedAt.ShouldBe(syncedAt);
    }

    [Fact]
    public void Succeeded_normalizes_timestamp_to_utc_and_clears_error()
    {
        var connection = Connected();
        connection.Failed("scheduling.calendar_provider_unavailable");
        var localOffset = new DateTimeOffset(2026, 10, 22, 12, 30, 0, TimeSpan.FromHours(-3));

        connection.Succeeded(localOffset);

        connection.LastSyncedAt.ShouldBe(new DateTimeOffset(2026, 10, 22, 15, 30, 0, TimeSpan.Zero));
        connection.LastSyncedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        connection.LastErrorCode.ShouldBeNull();
    }

    [Fact]
    public void Failed_stores_error_code()
    {
        var connection = Connected();

        connection.Failed("scheduling.calendar_remote_conflict");

        connection.LastErrorCode.ShouldBe("scheduling.calendar_remote_conflict");
    }

    [Fact]
    public void Remember_updates_baseline_and_remote_id_and_clears_pending_decision()
    {
        var link = CalendarSyncLink.Create(Guid.NewGuid(), CalendarItemKind.Event, Guid.NewGuid(), Guid.NewGuid(), "remote-old");
        link.Remember("v1", "snapshot-v1", "fingerprint-v1");
        link.Pend(CalendarPendingReason.Conflict, "pending-v2", "local-v2");

        link.Remember("v2", "snapshot-v2", "fingerprint-v2", "remote-new");

        link.RemoteId.ShouldBe("remote-new");
        link.RemoteVersion.ShouldBe("v2");
        link.RemoteSnapshot.ShouldBe("snapshot-v2");
        link.LocalFingerprint.ShouldBe("fingerprint-v2");
        link.PendingReason.ShouldBeNull();
        link.PendingSnapshot.ShouldBeNull();
        link.PendingLocalFingerprint.ShouldBeNull();
    }

    [Fact]
    public void Pend_preserves_baseline_and_stores_decision_snapshot_and_fingerprint()
    {
        var link = CalendarSyncLink.Create(Guid.NewGuid(), CalendarItemKind.Deadline, Guid.NewGuid(), null, "remote-id");
        link.Remember("baseline-v3", "baseline-snapshot", "baseline-fingerprint");

        link.Pend(CalendarPendingReason.ExternalDeletion, "decision-snapshot", "decision-fingerprint");

        link.RemoteId.ShouldBe("remote-id");
        link.RemoteVersion.ShouldBe("baseline-v3");
        link.RemoteSnapshot.ShouldBe("baseline-snapshot");
        link.LocalFingerprint.ShouldBe("baseline-fingerprint");
        link.PendingReason.ShouldBe(CalendarPendingReason.ExternalDeletion);
        link.PendingSnapshot.ShouldBe("decision-snapshot");
        link.PendingLocalFingerprint.ShouldBe("decision-fingerprint");
    }

    private static CalendarConnection Connected() =>
        CalendarConnection.Connect(UserId, CalendarProvider.Google, From, From.AddDays(10)).Value;
}

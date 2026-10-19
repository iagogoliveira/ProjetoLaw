using LexFlow.Platform.Domain.Notifications;

namespace LexFlow.Platform.UnitTests;

public sealed class NotificationTests
{
    [Fact]
    public void Notification_and_receipt_keep_only_identifiers_and_utc_timestamps()
    {
        var now = new DateTimeOffset(2026, 10, 19, 7, 0, 0, TimeSpan.FromHours(-3));
        var resource = Guid.CreateVersion7();
        var caseId = Guid.CreateVersion7();
        var notification = Notification.Create("publication:test", "publications.received", resource, caseId, now);
        notification.ResourceId.ShouldBe(resource);
        notification.CaseId.ShouldBe(caseId);
        notification.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
        var user = Guid.CreateVersion7();
        var receipt = NotificationReceipt.Read(notification.Id, user, caseId, now);
        receipt.NotificationId.ShouldBe(notification.Id);
        receipt.UserId.ShouldBe(user);
        receipt.CaseId.ShouldBe(caseId);
        receipt.ReadAt.ShouldBe(now.ToUniversalTime());
    }

    [Fact]
    public void Empty_identifiers_and_oversized_metadata_are_rejected()
    {
        var now = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);
        Should.Throw<ArgumentException>(() => Notification.Create("", "type", Guid.CreateVersion7(), null, now));
        Should.Throw<ArgumentException>(() => Notification.Create("key", "", Guid.CreateVersion7(), null, now));
        Should.Throw<ArgumentException>(() => Notification.Create(new string('x', 201), "type", Guid.CreateVersion7(), null, now));
        Should.Throw<ArgumentException>(() => NotificationReceipt.Read(Guid.Empty, Guid.CreateVersion7(), null, now));
    }
}

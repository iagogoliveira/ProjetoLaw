using LexFlow.Platform.Domain.Notifications;

namespace LexFlow.Platform.UnitTests;

public sealed class NotificationSettingsDomainTests
{
    [Fact]
    public void Preferences_are_opt_in_and_keep_section_choices_and_muted_kinds()
    {
        var user = Guid.CreateVersion7();
        var preference = NotificationPreference.Create(user);
        preference.UserId.ShouldBe(user);
        preference.DailyDigestEnabled.ShouldBeFalse();
        preference.IncludeAgenda.ShouldBeTrue();
        preference.IncludeDeadlines.ShouldBeTrue();
        preference.IncludePublications.ShouldBeTrue();
        preference.Set(true, false, true, false, ["publications.received"]).IsSuccess.ShouldBeTrue();
        preference.DailyDigestEnabled.ShouldBeTrue();
        preference.IncludeAgenda.ShouldBeFalse();
        preference.IncludePublications.ShouldBeFalse();
        preference.MutedKinds.ShouldBe(["publications.received"]);
    }

    [Fact]
    public void Empty_user_cannot_have_preferences() => Should.Throw<ArgumentException>(() => NotificationPreference.Create(Guid.Empty));

    [Fact]
    public void Rules_always_include_the_mandatory_floor()
    {
        var rule = NotificationRule.Create();
        rule.Set(["publications.received"]).IsSuccess.ShouldBeTrue();
        foreach (var kind in NotificationKinds.Mandatory) { rule.RequiredKinds.ShouldContain(kind); }
        rule.RequiredKinds.ShouldContain("publications.received");
        rule.Set([]).IsSuccess.ShouldBeTrue();
        rule.RequiredKinds.ShouldBe(NotificationKinds.Mandatory, ignoreOrder: true);
    }

    [Fact]
    public void Invalid_settings_do_not_mutate_preferences_or_rules()
    {
        var preference = NotificationPreference.Create(Guid.CreateVersion7());
        var rule = NotificationRule.Create();
        preference.Set(true, false, false, false, null).Error!.Code.ShouldBe("notifications.settings_invalid");
        preference.DailyDigestEnabled.ShouldBeFalse();
        preference.Set(true, true, true, true, ["unknown"]).IsFailure.ShouldBeTrue();
        rule.Set(null).IsFailure.ShouldBeTrue();
        rule.Set(["publications.received", "publications.received"]).IsFailure.ShouldBeTrue();
        rule.Set(["unknown"]).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Digest_delivery_records_day_user_and_utc_completion()
    {
        var user = Guid.CreateVersion7();
        var day = new DateOnly(2026, 10, 21);
        var delivery = DigestDelivery.Create(user, day);
        delivery.UserId.ShouldBe(user);
        delivery.Day.ShouldBe(day);
        delivery.SentAt.ShouldBeNull();
        delivery.MarkSent(new DateTimeOffset(2026, 10, 21, 7, 30, 0, TimeSpan.FromHours(-3)));
        delivery.SentAt.ShouldBe(new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero));
        delivery.SentAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }
}

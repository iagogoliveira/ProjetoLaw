using LexFlow.Publications.Application.Movements;
using LexFlow.Publications.Domain;

namespace LexFlow.Publications.UnitTests;

public sealed class MovementSyncTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public void Configuration_rejects_missing_key_without_changing_previous_values()
    {
        var value = MovementIntegration.Create();
        value.Configure(MovementProviderKind.Fake, null).IsSuccess.ShouldBeTrue();
        var revision = value.Revision;
        value.Configure(MovementProviderKind.DataJud, null).Error!.Code.ShouldBe("publications.integration_invalid");
        value.Provider.ShouldBe(MovementProviderKind.Fake);
        value.Revision.ShouldBe(revision);
        value.Configure((MovementProviderKind)99, "protected").IsFailure.ShouldBeTrue();
        value.Configure(MovementProviderKind.DataJud, "protected").IsSuccess.ShouldBeTrue();
        value.ProtectedApiKey.ShouldBe("protected");
        value.Revision.ShouldNotBe(revision);
    }
    [Fact]
    public void Failed_page_preserves_checkpoint_until_configuration_changes()
    {
        var id = Guid.CreateVersion7();
        var state = MovementSync.Create(id);
        var revision = Guid.CreateVersion7();
        state.ResetFor(revision);
        state.Attempt(Now);
        state.AcceptPage("tjsp", "next", Now, Now);
        state.Fail("publications.provider_unavailable");
        state.Cursor.ShouldBe("next");
        state.LastSuccessAt.ShouldBeNull();
        state.PageCount.ShouldBe(1);
        state.ResetFor(revision);
        state.Cursor.ShouldBe("next");
        state.ResetFor(Guid.CreateVersion7());
        state.Cursor.ShouldBeNull();
        state.ErrorCode.ShouldBeNull();
        state.PageCount.ShouldBe(0);
        state.AcceptPage("tjsp", null, Now, Now);
        state.LastSuccessAt.ShouldBe(Now);
        state.CompletedSlot.ShouldBe(Now);
        state.Restart();
        state.CompletedSlot.ShouldBeNull();
        state.Cursor.ShouldBeNull();
    }
    [Fact]
    public void Schedule_retries_pages_and_distributes_six_hour_windows_stably()
    {
        var id = Guid.Parse("aabbccdd-0000-0000-0000-000000000001");
        var revision = Guid.CreateVersion7();
        MovementSchedule.IsDue(null, revision, id, Now).ShouldBeTrue();
        var slot = MovementSchedule.Slot(id, Now);
        (Now - slot).ShouldBeLessThan(TimeSpan.FromHours(6));
        MovementSchedule.Slot(id, Now.AddHours(6)).ShouldBe(slot.AddHours(6));
        var state = MovementSync.Create(id);
        state.ResetFor(revision);
        state.Attempt(Now);
        state.AcceptPage("tjsp", "cursor", slot, Now);
        MovementSchedule.IsDue(state, revision, id, Now.AddMinutes(4)).ShouldBeFalse();
        MovementSchedule.IsDue(state, revision, id, Now.AddMinutes(5)).ShouldBeTrue();
        state.AcceptPage("tjsp", null, slot, Now);
        MovementSchedule.IsDue(state, revision, id, Now).ShouldBeFalse();
        MovementSchedule.IsDue(state, revision, id, Now.AddHours(6)).ShouldBeTrue();
        MovementSchedule.IsDue(state, Guid.CreateVersion7(), id, Now).ShouldBeTrue();
        Enumerable.Range(1, 200).Select(i => MovementSchedule.Slot(new Guid(i, 0, 0, new byte[8]), Now)).Distinct().Count().ShouldBeGreaterThan(100);
    }
    [Fact]
    public void Imported_movement_preserves_event_date_and_rejects_invalid_external_ids()
    {
        var id = Guid.CreateVersion7();
        var input = new MovementInput(Now.AddYears(-6).ToOffset(TimeSpan.FromHours(-3)), "Distribuição");
        var value = CaseMovement.Import(id, input, Now, "external").Value;
        value.OccurredAt.ShouldBe(input.OccurredAt.ToUniversalTime());
        value.CollectedAt.ShouldBe(Now);
        value.ExternalId.ShouldBe("external");
        value.Origin.ShouldBe(MovementOrigin.DataJud);
        CaseMovement.Import(id, input, Now, "").IsFailure.ShouldBeTrue();
        CaseMovement.Import(id, input, Now, new string('x', 129)).IsFailure.ShouldBeTrue();
        CaseMovement.Import(Guid.Empty, input, Now, "external").IsFailure.ShouldBeTrue();
    }
}

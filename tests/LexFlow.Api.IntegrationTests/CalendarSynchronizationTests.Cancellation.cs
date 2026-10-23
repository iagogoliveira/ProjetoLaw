using System.Net;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class CalendarSynchronizationTests
{
    [Fact]
    public async Task Local_cancellation_and_external_edit_require_conflict_decision_without_exporting_cancellation()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        await SyncAsync(owner, connection);
        var remote = _provider.Snapshot(Context(owner)).Single() with { Version = "2", StartsAt = Start.AddHours(3), EndsAt = Start.AddHours(4) };
        (await SendAsync(owner, HttpMethod.Post, $"/api/v1/schedule/events/{id}/cancel")).Status.ShouldBe(HttpStatusCode.OK);
        _provider.Seed(Context(owner), [remote]);
        await SyncAsync(owner, connection);
        _provider.Snapshot(Context(owner)).Single().ShouldBe(remote);
        var pending = (await PendingAsync(owner, connection)).EnumerateArray().Single();
        pending.GetProperty("reason").GetString().ShouldBe("conflict");
        (await ReadEventAsync(owner, id)).GetProperty("status").GetString().ShouldBe("cancelled");
        var kept = await ResolveAsync(owner, connection, pending.GetProperty("id").GetGuid(), "keepLocal");
        kept.Status.ShouldBe(HttpStatusCode.OK, kept.Body.ToString());
        _provider.Snapshot(Context(owner)).Single().IsCancelled.ShouldBeTrue();
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
    }
}

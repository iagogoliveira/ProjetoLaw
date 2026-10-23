using System.Net;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class CalendarSynchronizationTests
{
    [Fact]
    public async Task Unsupported_external_item_deleted_after_pending_can_be_ignored_without_stale_decision()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        _provider.Seed(Context(owner), [Remote() with { IsRecurring = true }]);
        await SyncAsync(owner, connection);
        var initial = (await PendingAsync(owner, connection)).EnumerateArray().Single();
        initial.GetProperty("reason").GetString().ShouldBe("unsupported");
        initial.GetProperty("localId").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        _provider.Seed(Context(owner), []);
        await SyncAsync(owner, connection);
        var refreshed = (await PendingAsync(owner, connection)).EnumerateArray().Single();
        refreshed.GetProperty("externalCancelled").GetBoolean().ShouldBeTrue();
        var ignored = await ResolveAsync(owner, connection, refreshed.GetProperty("id").GetGuid(), "keepLocal");
        ignored.Status.ShouldBe(HttpStatusCode.OK, ignored.Body.ToString());
        ignored.Body.GetProperty("resolved").GetBoolean().ShouldBeTrue();
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
        _provider.Snapshot(Context(owner)).Count.ShouldBe(0);
    }
}

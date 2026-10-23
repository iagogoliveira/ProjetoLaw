using System.Net;
using LexFlow.Scheduling.Domain.CalendarSync;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class CalendarSynchronizationTests
{
    [Fact]
    public async Task Pending_results_are_paged_without_dropping_unsupported_external_items()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        _options.PageSize = 100;
        _provider.Seed(Context(owner), Enumerable.Range(0, 101).Select(i => Remote($"recurring-{i:D3}") with { IsRecurring = true }).ToArray());
        await SyncAsync(owner, connection);
        var first = await SendAsync(owner, HttpMethod.Get, $"/api/v1/me/calendar-connections/{connection}/pending");
        first.Status.ShouldBe(HttpStatusCode.OK, first.Body.ToString());
        first.Body.GetProperty("items").GetArrayLength().ShouldBe(100);
        var cursor = first.Body.GetProperty("nextCursor").GetGuid();
        var second = await SendAsync(owner, HttpMethod.Get, $"/api/v1/me/calendar-connections/{connection}/pending?after={cursor}");
        second.Status.ShouldBe(HttpStatusCode.OK, second.Body.ToString());
        second.Body.GetProperty("items").GetArrayLength().ShouldBe(1);
        second.Body.GetProperty("nextCursor").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        var firstIds = first.Body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        firstIds.ShouldNotContain(second.Body.GetProperty("items")[0].GetProperty("id").GetGuid());
        await SyncAsync(owner, connection);
        _provider.Snapshot(Context(owner)).Count.ShouldBe(101);
    }

    [Fact]
    public async Task Redistributing_event_hides_old_pending_and_prevents_old_owner_export()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var destination = await TestOrganizations.AddMemberAsync(_dependencies, owner.OrganizationId, Guid.CreateVersion7(), Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        await SyncAsync(owner, connection);
        var remote = _provider.Snapshot(Context(owner)).Single() with { Version = "2", IsCancelled = true };
        _provider.Seed(Context(owner), [remote]);
        await SyncAsync(owner, connection);
        var pendingId = (await PendingAsync(owner, connection)).EnumerateArray().Single().GetProperty("id").GetGuid();
        var redistributed = await SendAsync(owner, HttpMethod.Post, "/api/v1/schedule/events/redistribute", new { ids = new[] { id }, assigneeUserId = destination.UserId });
        redistributed.Status.ShouldBe(HttpStatusCode.OK, redistributed.Body.ToString());
        redistributed.Body.GetProperty("items")[0].GetProperty("changed").GetBoolean().ShouldBeTrue();
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
        (await ResolveAsync(owner, connection, pendingId, "acceptExternal")).Status.ShouldBe(HttpStatusCode.NotFound);
        await EventAsync(destination, id, hours: 4);
        await SyncAsync(owner, connection);
        _provider.Snapshot(Context(owner)).Single().ShouldBe(remote);
        var local = await ReadEventAsync(destination, id);
        local.GetProperty("status").GetString().ShouldBe("scheduled");
        local.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(Start.AddHours(4));
    }
}

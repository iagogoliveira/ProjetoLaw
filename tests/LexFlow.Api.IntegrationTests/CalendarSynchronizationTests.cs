using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Scheduling.Application.CalendarSync;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.Scheduling.Infrastructure.CalendarSync;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class CalendarSynchronizationTests : IAsyncLifetime
{
    private static readonly string[] Permissions = ["identity.profile.read", "identity.profile.manage", "schedule.events.read", "schedule.events.manage"];
    private static readonly DateTimeOffset Start = new(2027, 4, 10, 13, 0, 0, TimeSpan.Zero);
    private readonly DependenciesFixture _dependencies;
    private readonly FakeCalendarSyncOptions _options = new() { Enabled = true, PageSize = 2 };
    private readonly FakeCalendarSyncProvider _provider;
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public CalendarSynchronizationTests(DependenciesFixture dependencies)
    {
        _dependencies = dependencies;
        _provider = new FakeCalendarSyncProvider(_options);
    }

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(_dependencies.Settings, replaceServices: services =>
        {
            services.RemoveAll<FakeCalendarSyncOptions>();
            services.AddSingleton(_options);
            services.RemoveAll<ICalendarSyncProvider>();
            services.AddSingleton<ICalendarSyncProvider>(_provider);
        });
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember owner, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, owner.UserId, owner.OrganizationId, body);

    private static CalendarSyncContext Context(SeededMember owner, CalendarProvider provider = CalendarProvider.Google) => new(owner.OrganizationId, owner.UserId, provider);
    private static RemoteCalendarEvent Remote(string id = "external") => new(id, "1", "External meeting", "External description", "External room", Start, Start.AddHours(1), null, false, false, null);

    private async Task<Guid> ConnectAsync(SeededMember owner, string provider = "google")
    {
        var result = await SendAsync(owner, HttpMethod.Post, "/api/v1/me/calendar-connections", new { provider, from = "2027-04-01", until = "2027-05-01" });
        result.Status.ShouldBe(HttpStatusCode.OK, result.Body.ToString());
        return result.Body.GetProperty("id").GetGuid();
    }

    private async Task SyncAsync(SeededMember owner, Guid connection)
    {
        var result = await SendAsync(owner, HttpMethod.Post, $"/api/v1/me/calendar-connections/{connection}/sync");
        result.Status.ShouldBe(HttpStatusCode.Accepted, result.Body.ToString());
        await _factory.RunJobsAsync(owner.OrganizationId);
    }

    private async Task<Guid> EventAsync(SeededMember owner, Guid? id = null, string title = "Private title", int hours = 0)
    {
        var result = await SendAsync(owner, id is null ? HttpMethod.Post : HttpMethod.Put, $"/api/v1/schedule/events{(id is null ? string.Empty : $"/{id}")}", new
        {
            title,
            description = "Private description",
            location = "Private room",
            startsAt = Start.AddHours(hours),
            endsAt = Start.AddHours(hours + 1),
            assigneeUserId = owner.UserId,
        });
        result.Status.ShouldBe(id is null ? HttpStatusCode.Created : HttpStatusCode.OK, result.Body.ToString());
        return result.Body.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> ReadEventAsync(SeededMember owner, Guid id)
    {
        var result = await SendAsync(owner, HttpMethod.Get, $"/api/v1/schedule/events/{id}");
        result.Status.ShouldBe(HttpStatusCode.OK, result.Body.ToString());
        return result.Body;
    }

    private async Task<JsonElement> PendingAsync(SeededMember owner, Guid connection)
    {
        var result = await SendAsync(owner, HttpMethod.Get, $"/api/v1/me/calendar-connections/{connection}/pending");
        result.Status.ShouldBe(HttpStatusCode.OK, result.Body.ToString());
        return result.Body.GetProperty("items");
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> ResolveAsync(SeededMember owner, Guid connection, Guid pending, string resolution) =>
        SendAsync(owner, HttpMethod.Post, $"/api/v1/me/calendar-connections/{connection}/pending/{pending}/resolve", new { resolution });

    private async Task<Guid> ImportedIdAsync(SeededMember owner)
    {
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "calendar-sync-test");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(owner.OrganizationId);
        return await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Events.Select(e => e.Id).SingleAsync();
    }

    [Theory]
    [InlineData(CalendarProvider.Google, "google")]
    [InlineData(CalendarProvider.Microsoft365, "microsoft365")]
    public async Task Normalized_provider_events_import_update_and_replay_without_duplicates(CalendarProvider provider, string providerName)
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner, providerName);
        var context = Context(owner, provider);
        _provider.Seed(context, [Remote()]);
        await SyncAsync(owner, connection);
        var id = await ImportedIdAsync(owner);
        (await ReadEventAsync(owner, id)).GetProperty("title").GetString().ShouldBe("External meeting");
        _provider.Seed(context, [Remote() with { Version = "2", Title = "Updated externally", StartsAt = Start.AddHours(2), EndsAt = Start.AddHours(3) }]);
        await SyncAsync(owner, connection);
        await SyncAsync(owner, connection);
        (await ImportedIdAsync(owner)).ShouldBe(id);
        var updated = await ReadEventAsync(owner, id);
        updated.GetProperty("title").GetString().ShouldBe("Updated externally");
        updated.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(Start.AddHours(2));
        _provider.Snapshot(context).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Generic_export_roundtrip_preserves_private_content_and_remote_text_on_local_write()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        await SyncAsync(owner, connection);
        var remote = _provider.Snapshot(Context(owner)).Single();
        remote.Title.ShouldBe("Compromisso LexFlow");
        remote.Description.ShouldBeNull();
        remote.Location.ShouldBeNull();
        _provider.Seed(Context(owner), [remote with { Version = "2", StartsAt = Start.AddHours(2), EndsAt = Start.AddHours(3) }]);
        await SyncAsync(owner, connection);
        var local = await ReadEventAsync(owner, id);
        local.GetProperty("title").GetString().ShouldBe("Private title");
        local.GetProperty("description").GetString().ShouldBe("Private description");
        local.GetProperty("location").GetString().ShouldBe("Private room");
        local.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(Start.AddHours(2));
        await EventAsync(owner, id, hours: 4);
        await SyncAsync(owner, connection);
        var exported = _provider.Snapshot(Context(owner)).Single();
        exported.Title.ShouldBe(remote.Title);
        exported.Description.ShouldBeNull();
        exported.StartsAt.ShouldBe(Start.AddHours(4));
    }

    [Fact]
    public async Task Conflict_requires_decision_and_accepting_only_external_deltas_preserves_local_title()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        await SyncAsync(owner, connection);
        var remote = _provider.Snapshot(Context(owner)).Single();
        await EventAsync(owner, id, "Locally revised title", 1);
        _provider.Seed(Context(owner), [remote with { Version = "2", StartsAt = Start.AddHours(3), EndsAt = Start.AddHours(4) }]);
        await SyncAsync(owner, connection);
        var pending = (await PendingAsync(owner, connection)).EnumerateArray().Single();
        pending.GetProperty("reason").GetString().ShouldBe("conflict");
        (await ReadEventAsync(owner, id)).GetProperty("startsAt").GetDateTimeOffset().ShouldBe(Start.AddHours(1));
        var result = await ResolveAsync(owner, connection, pending.GetProperty("id").GetGuid(), "acceptExternal");
        result.Status.ShouldBe(HttpStatusCode.OK, result.Body.ToString());
        var updated = await ReadEventAsync(owner, id);
        updated.GetProperty("title").GetString().ShouldBe("Locally revised title");
        updated.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(Start.AddHours(3));
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [InlineData(true, "acceptExternal")]
    [InlineData(false, "keepLocal")]
    public async Task Missing_external_event_requires_explicit_decision(bool accept, string resolution)
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        await SyncAsync(owner, connection);
        _provider.Seed(Context(owner), []);
        await SyncAsync(owner, connection);
        var pending = (await PendingAsync(owner, connection)).EnumerateArray().Single();
        pending.GetProperty("reason").GetString().ShouldBe("externalDeletion");
        (await ReadEventAsync(owner, id)).GetProperty("status").GetString().ShouldBe("scheduled");
        var result = await ResolveAsync(owner, connection, pending.GetProperty("id").GetGuid(), resolution);
        result.Status.ShouldBe(HttpStatusCode.OK, result.Body.ToString());
        (await ReadEventAsync(owner, id)).GetProperty("status").GetString().ShouldBe(accept ? "cancelled" : "scheduled");
        if (!accept)
        {
            var recreated = _provider.Snapshot(Context(owner)).Single();
            recreated.IsCancelled.ShouldBeFalse();
            recreated.StartsAt.ShouldBe(Start);
            await SyncAsync(owner, connection);
            _provider.Snapshot(Context(owner)).Count.ShouldBe(1);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Decision_rejects_changed_local_or_remote_version_until_resynchronized(bool changeLocal)
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = await EventAsync(owner);
        await SyncAsync(owner, connection);
        var remote = _provider.Snapshot(Context(owner)).Single() with { Version = "2", IsCancelled = true };
        _provider.Seed(Context(owner), [remote]);
        await SyncAsync(owner, connection);
        var pending = (await PendingAsync(owner, connection)).EnumerateArray().Single().GetProperty("id").GetGuid();
        if (changeLocal) { await EventAsync(owner, id, "Changed after decision", 2); }
        else { _provider.Seed(Context(owner), [remote with { Version = "3", Title = "Changed after decision" }]); }
        var stale = await ResolveAsync(owner, connection, pending, "acceptExternal");
        stale.Status.ShouldBe(HttpStatusCode.Conflict, stale.Body.ToString());
        OrganizationApi.Code(stale.Body).ShouldBe("scheduling.calendar_decision_stale");
        await SyncAsync(owner, connection);
        (await ResolveAsync(owner, connection, pending, "acceptExternal")).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Event_moved_outside_read_window_is_read_individually_instead_of_inferred_deleted()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        _provider.Seed(Context(owner), [Remote()]);
        await SyncAsync(owner, connection);
        var id = await ImportedIdAsync(owner);
        var outside = Remote() with { Version = "2", StartsAt = Start.AddMonths(2), EndsAt = Start.AddMonths(2).AddHours(1) };
        _provider.Seed(Context(owner), [outside]);
        await SyncAsync(owner, connection);
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
        (await ReadEventAsync(owner, id)).GetProperty("startsAt").GetDateTimeOffset().ShouldBe(outside.StartsAt);
    }

    [Fact]
    public async Task Unsupported_recurrence_and_all_day_are_pending_and_pages_are_fully_imported()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        _provider.Seed(Context(owner), [Remote("recurrence") with { IsRecurring = true }, Remote("all-day") with { Day = new DateOnly(2027, 4, 10) }, Remote("valid")]);
        await SyncAsync(owner, connection);
        var pending = await PendingAsync(owner, connection);
        pending.GetArrayLength().ShouldBe(2);
        foreach (var item in pending.EnumerateArray()) { item.GetProperty("reason").GetString().ShouldBe("unsupported"); }
        (await ReadEventAsync(owner, await ImportedIdAsync(owner))).GetProperty("title").GetString().ShouldBe("External meeting");
    }

    [Fact]
    public async Task Read_only_member_cannot_import_remote_events()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, ["identity.profile.read", "identity.profile.manage", "schedule.events.read"]);
        var connection = await ConnectAsync(owner);
        _provider.Seed(Context(owner), [Remote()]);
        await SyncAsync(owner, connection);
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "calendar-readonly-test");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(owner.OrganizationId);
        (await scope.ServiceProvider.GetRequiredService<SchedulingDbContext>().Events.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
        _provider.Snapshot(Context(owner)).Single().Title.ShouldBe("External meeting");
    }
}

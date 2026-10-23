using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.Scheduling.Infrastructure.CalendarSync;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexFlow.Api.IntegrationTests;

public sealed class CalendarConnectionTests : IAsyncLifetime
{
    private static readonly string[] Permissions = ["identity.profile.read", "identity.profile.manage", "schedule.events.read", "schedule.events.manage"];
    private static readonly DateOnly From = new(2027, 4, 1);
    private static readonly DateOnly Until = new(2027, 5, 1);
    private readonly DependenciesFixture _dependencies;
    private readonly FakeCalendarSyncOptions _options = new() { Enabled = true, Available = true };
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public CalendarConnectionTests(DependenciesFixture dependencies) => _dependencies = dependencies;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(_dependencies.Settings, replaceServices: services =>
        {
            services.RemoveAll<FakeCalendarSyncOptions>();
            services.AddSingleton(_options);
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

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    private Task<(HttpStatusCode Status, JsonElement Body)> ConnectAsync(SeededMember member, string provider = "google", object? from = null, object? until = null) =>
        SendAsync(member, HttpMethod.Post, "/api/v1/me/calendar-connections", new { provider, from = from ?? From.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), until = until ?? Until.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });

    [Fact]
    public async Task Connections_are_per_user_and_provider_and_reactivation_keeps_id()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var colleague = await TestOrganizations.AddMemberAsync(_dependencies, owner.OrganizationId, Guid.CreateVersion7(), Permissions);
        var sameUserOtherOffice = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var first = await ConnectAsync(owner);
        var repeated = await ConnectAsync(owner);
        var microsoft = await ConnectAsync(owner, "microsoft365");
        var colleagueConnection = await ConnectAsync(colleague);
        var otherOfficeConnection = await ConnectAsync(sameUserOtherOffice);

        first.Status.ShouldBe(HttpStatusCode.OK, first.Body.ToString());
        repeated.Body.GetProperty("id").GetGuid().ShouldBe(first.Body.GetProperty("id").GetGuid());
        microsoft.Status.ShouldBe(HttpStatusCode.OK, microsoft.Body.ToString());
        colleagueConnection.Status.ShouldBe(HttpStatusCode.OK);
        otherOfficeConnection.Status.ShouldBe(HttpStatusCode.OK);
        var (_, list) = await SendAsync(owner, HttpMethod.Get, "/api/v1/me/calendar-connections");
        list.GetProperty("items").GetArrayLength().ShouldBe(2);
        var id = first.Body.GetProperty("id").GetGuid();
        var disconnected = await SendAsync(owner, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/disconnect");
        var queued = await SendAsync(owner, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/sync");
        disconnected.Status.ShouldBe(HttpStatusCode.OK);
        queued.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(queued.Body).ShouldBe("scheduling.calendar_connection_inactive");
        var reconnected = await ConnectAsync(owner);
        reconnected.Body.GetProperty("id").GetGuid().ShouldBe(id);
        reconnected.Body.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Connection_and_pending_routes_hide_resources_from_other_users_and_offices()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var colleague = await TestOrganizations.AddMemberAsync(_dependencies, owner.OrganizationId, Guid.CreateVersion7(), Permissions);
        var otherOffice = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = connection.Body.GetProperty("id").GetGuid();
        var pendingId = Guid.CreateVersion7();

        foreach (var member in new[] { colleague, otherOffice })
        {
            var disconnect = await SendAsync(member, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/disconnect");
            var sync = await SendAsync(member, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/sync");
            var pending = await SendAsync(member, HttpMethod.Get, $"/api/v1/me/calendar-connections/{id}/pending");
            var resolve = await SendAsync(member, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/pending/{pendingId}/resolve", new { resolution = "keepLocal" });
            disconnect.Status.ShouldBe(HttpStatusCode.NotFound);
            sync.Status.ShouldBe(HttpStatusCode.NotFound);
            pending.Status.ShouldBe(HttpStatusCode.NotFound);
            resolve.Status.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Non_member_cannot_read_or_change_calendar_connections()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var reader = new SeededMember(owner.OrganizationId, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var connection = await ConnectAsync(owner);
        var id = connection.Body.GetProperty("id").GetGuid();
        var (listStatus, _) = await SendAsync(reader, HttpMethod.Get, "/api/v1/me/calendar-connections");
        var (pendingStatus, _) = await SendAsync(reader, HttpMethod.Get, $"/api/v1/me/calendar-connections/{id}/pending");
        var (createStatus, createBody) = await ConnectAsync(reader);
        var (disconnectStatus, _) = await SendAsync(reader, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/disconnect");
        var (syncStatus, _) = await SendAsync(reader, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/sync");

        listStatus.ShouldBe(HttpStatusCode.Forbidden);
        pendingStatus.ShouldBe(HttpStatusCode.Forbidden);
        createStatus.ShouldBe(HttpStatusCode.Forbidden, createBody.ToString());
        disconnectStatus.ShouldBe(HttpStatusCode.Forbidden);
        syncStatus.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Connect_validates_provider_window_and_availability()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        _options.Enabled = false;
        var unavailable = await ConnectAsync(owner);
        unavailable.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(unavailable.Body).ShouldBe("scheduling.calendar_provider_unavailable");
        _options.Enabled = true;

        var invalidProvider = await ConnectAsync(owner, "invalid");
        var defaultDates = await ConnectAsync(owner, from: "0001-01-01", until: "0001-01-01");
        var invalidDate = await ConnectAsync(owner, from: "bad-date");
        var tooLong = await ConnectAsync(owner, from: "2027-01-01", until: "2028-01-03");
        var maximum = await ConnectAsync(owner, "microsoft365", "2027-01-01", "2028-01-02");
        invalidProvider.Status.ShouldBe(HttpStatusCode.BadRequest);
        defaultDates.Status.ShouldBe(HttpStatusCode.BadRequest);
        invalidDate.Status.ShouldBe(HttpStatusCode.BadRequest);
        tooLong.Status.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(tooLong.Body).ShouldBe("scheduling.calendar_connection_invalid");
        maximum.Status.ShouldBe(HttpStatusCode.OK, maximum.Body.ToString());
    }

    [Fact]
    public async Task Sync_job_exports_only_generic_admin_event_and_is_idempotent()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, Permissions);
        var connection = await ConnectAsync(owner);
        var id = connection.Body.GetProperty("id").GetGuid();
        var starts = new DateTimeOffset(2027, 4, 10, 13, 0, 0, TimeSpan.Zero);
        var (eventStatus, calendarEvent) = await SendAsync(owner, HttpMethod.Post, "/api/v1/schedule/events", new
        {
            title = "Consulta sigilosa original",
            description = "Descrição privada original",
            startsAt = starts,
            endsAt = starts.AddHours(1),
            assigneeUserId = owner.UserId,
            location = "Sala privada original",
        });
        eventStatus.ShouldBe(HttpStatusCode.Created, calendarEvent.ToString());
        var (queueStatus, queue) = await SendAsync(owner, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/sync");
        queueStatus.ShouldBe(HttpStatusCode.Accepted, queue.ToString());
        await _factory.RunJobsAsync(owner.OrganizationId);
        var first = await ReadStateAsync(owner.OrganizationId, id);
        first.Connection.GetProperty("LastSyncedAt").ValueKind.ShouldBe(JsonValueKind.String);
        first.Connection.GetProperty("LastErrorCode").ValueKind.ShouldBe(JsonValueKind.Null);
        first.Items.Count.ShouldBe(1);
        var payload = first.Items[0].GetProperty("Payload");
        payload.GetProperty("Title").GetString().ShouldBe("Compromisso LexFlow");
        payload.GetProperty("Description").ValueKind.ShouldBe(JsonValueKind.Null);
        payload.GetProperty("Location").ValueKind.ShouldBe(JsonValueKind.Null);
        payload.ToString().ShouldNotContain("Consulta sigilosa original");
        payload.ToString().ShouldNotContain("Descrição privada original");
        payload.ToString().ShouldNotContain("Sala privada original");
        first.Items[0].GetProperty("Link").GetString().ShouldStartWith("http://localhost:4200/agenda/compromissos/");

        await SendAsync(owner, HttpMethod.Post, $"/api/v1/me/calendar-connections/{id}/sync");
        await _factory.RunJobsAsync(owner.OrganizationId);
        var second = await ReadStateAsync(owner.OrganizationId, id);
        second.Items.Count.ShouldBe(1);
    }

    private async Task<(JsonElement Connection, List<JsonElement> Items, List<JsonElement> Links)> ReadStateAsync(Guid organizationId, Guid connectionId)
    {
        using var ambient = AmbientContext.Begin(organizationId, "calendar-test-inspection");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(organizationId);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var connection = await db.CalendarConnections.AsNoTracking().SingleAsync(item => item.Id == connectionId);
        var items = await db.SimulatedCalendarItems.AsNoTracking().ToListAsync();
        return (JsonDocument.Parse(JsonSerializer.Serialize(new { connection.LastSyncedAt, connection.LastErrorCode })).RootElement.Clone(),
            items.Select(item => JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                Payload = JsonDocument.Parse(item.Payload).RootElement,
                item.Link,
            })).RootElement.Clone()).ToList(), []);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Infrastructure.Notifications;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Infrastructure.Capture;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using LexFlow.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class NotificationSettingsTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly string[] MandatoryKinds =
    [
        "scheduling.deadline_missed", "scheduling.calendar_changed", "scheduling.deadline_due_today",
    ];
    private static readonly string[] MutedKinds = ["publications.received", "scheduling.deadline_missed"];
    private static readonly string[] DuplicateKinds = ["publications.received", "publications.received"];
    private static readonly string[] UnknownKinds = ["unknown.kind"];
    private static readonly string[] PublicationKind = ["publications.received"];
    private static readonly string[] ClientRole = ["client"];
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private readonly FakeCaptureOptions _captureOptions = new();
    private readonly SettingsTestClock _clock = new();
    private LexFlowApiFactory _factory = null!;
    private HttpClient _client = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(dependencies.Settings, replaceServices: services =>
        {
            services.AddSingleton(_captureOptions);
            services.AddSingleton<IClock>(_clock);
        });
        _client = _factory.CreateClient();
        _api = new OrganizationApi(_client);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Preferences_default_to_daily_digest_disabled_and_round_trip_per_user_and_tenant()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, "profile.read", "profile.manage");
        var peer = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "profile.read", "profile.manage");
        var otherOffice = await TestOrganizations.CreateWithMemberAsync(dependencies, "profile.read", "profile.manage");

        var (defaultStatus, defaults) = await SendAsync(office, HttpMethod.Get, "/api/v1/me/notification-settings");
        defaultStatus.ShouldBe(HttpStatusCode.OK, defaults.ToString());
        defaults.GetProperty("dailyDigestEnabled").GetBoolean().ShouldBeFalse();
        defaults.GetProperty("includeAgenda").GetBoolean().ShouldBeTrue();
        defaults.GetProperty("includeDeadlines").GetBoolean().ShouldBeTrue();
        defaults.GetProperty("includePublications").GetBoolean().ShouldBeTrue();

        var (status, saved) = await SendAsync(office, HttpMethod.Put, "/api/v1/me/notification-settings", new
        {
            dailyDigestEnabled = true,
            includeAgenda = false,
            includeDeadlines = true,
            includePublications = false,
            mutedKinds = MutedKinds,
        });
        status.ShouldBe(HttpStatusCode.OK, saved.ToString());
        saved.GetProperty("mutedKinds").EnumerateArray().Select(x => x.GetString()).ShouldBe(MutedKinds);
        saved.GetProperty("dailyDigestEnabled").GetBoolean().ShouldBeTrue();
        saved.GetProperty("includeAgenda").GetBoolean().ShouldBeFalse();

        (await SendAsync(peer, HttpMethod.Get, "/api/v1/me/notification-settings")).Body.GetProperty("dailyDigestEnabled").GetBoolean().ShouldBeFalse();
        (await SendAsync(otherOffice, HttpMethod.Get, "/api/v1/me/notification-settings")).Body.GetProperty("dailyDigestEnabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Preferences_and_rules_reject_null_duplicate_and_unknown_kind_lists()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, "profile.manage", "settings.organization.manage");
        var path = "/api/v1/me/notification-settings";
        var baseSettings = new { dailyDigestEnabled = false, includeAgenda = true, includeDeadlines = true, includePublications = true };

        (await SendAsync(office, HttpMethod.Put, path, new { baseSettings.dailyDigestEnabled, baseSettings.includeAgenda, baseSettings.includeDeadlines, baseSettings.includePublications, mutedKinds = (string[]?)null })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, path, new { baseSettings.dailyDigestEnabled, baseSettings.includeAgenda, baseSettings.includeDeadlines, baseSettings.includePublications, mutedKinds = DuplicateKinds })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, path, new { baseSettings.dailyDigestEnabled, baseSettings.includeAgenda, baseSettings.includeDeadlines, baseSettings.includePublications, mutedKinds = UnknownKinds })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, "/api/v1/organization/notification-rules", new { requiredKinds = (string[]?)null })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, "/api/v1/organization/notification-rules", new { requiredKinds = DuplicateKinds })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, "/api/v1/organization/notification-rules", new { requiredKinds = UnknownKinds })).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Organization_rules_keep_the_mandatory_floor_and_can_require_publications()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, "settings.organization.manage");
        var path = "/api/v1/organization/notification-rules";
        var (initialStatus, initial) = await SendAsync(office, HttpMethod.Get, path);
        initialStatus.ShouldBe(HttpStatusCode.OK, initial.ToString());
        initial.GetProperty("requiredKinds").EnumerateArray().Select(x => x.GetString()).ShouldBe(MandatoryKinds);
        initial.GetProperty("mandatoryKinds").EnumerateArray().Select(x => x.GetString()).ShouldBe(MandatoryKinds);

        var (setStatus, set) = await SendAsync(office, HttpMethod.Put, path, new { requiredKinds = PublicationKind });
        setStatus.ShouldBe(HttpStatusCode.OK, set.ToString());
        set.GetProperty("requiredKinds").EnumerateArray().Select(x => x.GetString()).ShouldContain("publications.received");
        set.GetProperty("requiredKinds").EnumerateArray().Select(x => x.GetString()).ShouldContain("scheduling.deadline_missed");

        var (resetStatus, reset) = await SendAsync(office, HttpMethod.Put, path, new { requiredKinds = Array.Empty<string>() });
        resetStatus.ShouldBe(HttpStatusCode.OK, reset.ToString());
        reset.GetProperty("requiredKinds").EnumerateArray().Select(x => x.GetString()).ShouldBe(MandatoryKinds);
    }

    [Fact]
    public async Task Settings_require_permission_active_membership_and_required_mfa()
    {
        var (organization, admin) = await _api.OnboardAsync();
        var member = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7(), "profile.read", "profile.manage");
        (await SendAsync(member, HttpMethod.Get, "/api/v1/me/notification-settings")).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(member, HttpMethod.Get, "/api/v1/organization/notification-rules")).Status.ShouldBe(HttpStatusCode.Forbidden);

        (await _api.SendAsync(HttpMethod.Put, "/api/v1/organization/security", admin, organization, new { mfaRequired = true })).Status.ShouldBe(HttpStatusCode.NoContent);
        (await SendAsync(member, HttpMethod.Get, "/api/v1/me/notification-settings")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(member, HttpMethod.Get, "/api/v1/me/notification-settings", amr: "pwd,mfa")).Status.ShouldBe(HttpStatusCode.OK);

        var removed = await SendAsync(admin, organization, HttpMethod.Delete, $"/api/v1/organization/members/{member.MemberId}", amr: "pwd,mfa");
        removed.Status.ShouldBe(HttpStatusCode.NoContent);
        (await SendAsync(member, HttpMethod.Get, "/api/v1/me/notification-settings", amr: "pwd,mfa")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Required_notifications_override_user_mutes_while_optional_publications_are_suppressed()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies,
            "profile.read", "profile.manage", "settings.organization.manage", "cases.create", "cases.read",
            "cases.edit", "cases.restrictions.manage", "crm.people.create", "schedule.deadlines.read", "schedule.deadlines.verify",
            "settings.integrations.manage", "publications.read");
        var sourceId = await _api.CreateAsync("/api/v1/publication-sources", office.UserId, office.OrganizationId,
            new { kind = "oab", label = "Fonte", value = "SP 123456", isActive = true });
        await OfficeScopeAsync(office, sp => sp.GetRequiredService<CaptureSourceJob>().RunAsync(sourceId, _clock.Today));
        await ConsumeCaptureEventsAsync(office);
        var caseId = await CreateCaseAsync(office);
        var (deadlineId, activityId) = await CreateMissedDeadlineAsync(office, caseId);
        await ConsumeAsync(new DeadlineAlertIntegrationEvent(activityId, deadlineId, caseId, "scheduling.deadline_missed")
        {
            EventId = Guid.CreateVersion7(),
            OrganizationId = office.OrganizationId,
            OccurredAt = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero),
        });

        var (beforeStatus, before) = await SendAsync(office, HttpMethod.Get, "/api/v1/notifications");
        beforeStatus.ShouldBe(HttpStatusCode.OK, before.ToString());
        before.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("kind").GetString()).ShouldContain("publications.received");
        before.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("kind").GetString()).ShouldContain("scheduling.deadline_missed");

        (await SendAsync(office, HttpMethod.Put, "/api/v1/me/notification-settings", new
        {
            dailyDigestEnabled = false,
            includeAgenda = true,
            includeDeadlines = true,
            includePublications = true,
            mutedKinds = MutedKinds,
        })).Status.ShouldBe(HttpStatusCode.OK);
        var (feedStatus, feed) = await SendAsync(office, HttpMethod.Get, "/api/v1/notifications");
        feedStatus.ShouldBe(HttpStatusCode.OK, feed.ToString());
        var items = feed.GetProperty("items").EnumerateArray().ToArray();
        items.Select(x => x.GetProperty("kind").GetString()).ShouldNotContain("publications.received");
        items.Select(x => x.GetProperty("kind").GetString()).ShouldContain("scheduling.deadline_missed");
    }

    private async Task OfficeScopeAsync(SeededMember office, Func<IServiceProvider, Task> work)
    {
        using var ambient = AmbientContext.Begin(office.OrganizationId, "notification-settings-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office.OrganizationId);
        await work(scope.ServiceProvider);
    }

    private async Task ConsumeCaptureEventsAsync(SeededMember office)
    {
        while (_factory.PublishedEvents.TryDequeue(out var original))
        {
            if (original is not PublicationCapturedIntegrationEvent captured) { continue; }
            var value = captured with { EventId = Guid.CreateVersion7(), OrganizationId = office.OrganizationId, OccurredAt = _clock.UtcNow };
            await OfficeScopeAsync(office, sp => ActivatorUtilities.CreateInstance<PublicationNotificationConsumer>(sp).Handle(value, Ct));
        }
    }

    private async Task<Guid> CreateCaseAsync(SeededMember office)
    {
        var personId = await _api.CreateAsync("/api/v1/people", office.UserId, office.OrganizationId,
            new { kind = "company", name = $"Cliente {Guid.CreateVersion7():N}", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", office.UserId, office.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = office.UserId } },
        });
    }

    private async Task<(Guid DeadlineId, Guid ActivityId)> CreateMissedDeadlineAsync(SeededMember office, Guid caseId)
    {
        using var ambient = AmbientContext.Begin(office.OrganizationId, "notification-settings-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office.OrganizationId);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var deadline = Deadline.Create(caseId, Guid.CreateVersion7(), "Notification settings test",
            new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 25), new DateOnly(2026, 11, 2), office.UserId, true,
            office.UserId, clock.UtcNow).Value;
        deadline.MarkMissed("Missed during integration test", office.UserId, clock.UtcNow).IsSuccess.ShouldBeTrue();
        var activity = DeadlineActivity.Record(deadline, "scheduling.deadline_missed", office.UserId, clock.UtcNow);
        db.Deadlines.Add(deadline);
        db.DeadlineActivities.Add(activity);
        await db.SaveChangesAsync(Ct);
        return (deadline.Id, activity.Id);
    }

    private async Task ConsumeAsync(DeadlineAlertIntegrationEvent integrationEvent)
    {
        using var ambient = AmbientContext.Begin(integrationEvent.OrganizationId, "notification-settings-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(integrationEvent.OrganizationId);
        var consumer = ActivatorUtilities.CreateInstance<DeadlineNotificationConsumer>(scope.ServiceProvider);
        await ((IIntegrationEventHandler<DeadlineAlertIntegrationEvent>)consumer).Handle(integrationEvent, Ct);
    }

    private sealed class SettingsTestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, LegalTimeZone).DateTime);
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null, string? amr = null)
        => SendAsync(member.UserId, member.OrganizationId, method, path, body, amr);

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(Guid userId, Guid organizationId, HttpMethod method, string path, object? body = null, string? amr = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, userId.ToString());
        request.Headers.Add(TestAuthenticationHandler.OrganizationHeader, organizationId.ToString());
        if (amr is not null) { request.Headers.Add(TestAuthenticationHandler.AmrHeader, amr); }
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return await SendRequestAsync(request);
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendRequestAsync(HttpRequestMessage request)
    {
        using (request)
        using (var response = await _client.SendAsync(request, Ct))
        {
            var text = await response.Content.ReadAsStringAsync(Ct);
            return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
        }
    }
}

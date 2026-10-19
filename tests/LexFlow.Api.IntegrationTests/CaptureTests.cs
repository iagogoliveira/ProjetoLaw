using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Platform.Domain.Notifications;
using LexFlow.Platform.Infrastructure.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Publications.Application;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Capture;
using LexFlow.Publications.Infrastructure.Persistence;
using LexFlow.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class CaptureTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] Permissions = ["settings.integrations.manage", "publications.read", "publications.triage", "cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create"];
    private static readonly string[] Readers = ["publications.read", "cases.read"];
    private static readonly string[] Controllers = ["publications.triage"];
    private static readonly string[] Integrators = ["settings.integrations.manage"];
    private static readonly string[] ClientRole = ["client"];
    private readonly CaptureClock _clock = new();
    private readonly FakeCaptureOptions _options = new();
    private readonly CaptureMail _mail = new();
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(dependencies.Settings, replaceServices: services =>
        {
            services.AddSingleton<IClock>(_clock);
            services.AddSingleton(_options);
            services.AddSingleton<IEmailSender>(_mail);
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
    private sealed class CaptureClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, LegalTimeZone).DateTime);
        public void Advance(TimeSpan interval) => UtcNow = UtcNow.Add(interval);
    }
    private sealed class CaptureMail : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Messages { get; } = new();
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Enqueue(message);
            return Task.CompletedTask;
        }
    }
    private Task<SeededMember> OfficeAsync() => TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);
    private Task<Guid> SourceAsync(SeededMember member, bool active = true) =>
        _api.CreateAsync("/api/v1/publication-sources", member.UserId, member.OrganizationId, new { kind = "oab", label = "Fonte", value = "SP 123456", isActive = active });
    private async Task OfficeScopeAsync(SeededMember member, Func<IServiceProvider, Task> work)
    {
        using var ambient = AmbientContext.Begin(member.OrganizationId, "capture-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(member.OrganizationId);
        await work(scope.ServiceProvider);
    }
    private Task CaptureAsync(SeededMember office, Guid source, bool force = false) =>
        OfficeScopeAsync(office, sp => sp.GetRequiredService<CaptureSourceJob>().RunAsync(source, _clock.Today, force));
    private Task MonitorAsync(SeededMember office, Guid source, DateOnly? day = null) =>
        OfficeScopeAsync(office, sp => sp.GetRequiredService<CaptureMonitor>().RunAsync(source, day ?? _clock.Today));

    private async Task ConsumeEventsAsync(SeededMember office)
    {
        while (_factory.PublishedEvents.TryDequeue(out var original))
        {
            var value = original with { EventId = Guid.CreateVersion7(), OrganizationId = office.OrganizationId, OccurredAt = _clock.UtcNow };
            await OfficeScopeAsync(office, async sp =>
            {
                switch (value)
                {
                    case SourceActivatedIntegrationEvent activated:
                        await ActivatorUtilities.CreateInstance<SourceActivatedConsumer>(sp).Handle(activated, Ct);
                        break;
                    case PublicationCapturedIntegrationEvent captured:
                        await ActivatorUtilities.CreateInstance<PublicationNotificationConsumer>(sp).Handle(captured, Ct);
                        break;
                    case CaptureMissingIntegrationEvent missing:
                        await ActivatorUtilities.CreateInstance<CaptureNotificationConsumer>(sp).Handle(missing, Ct);
                        await ActivatorUtilities.CreateInstance<CaptureEmailConsumer>(sp).Handle(missing, Ct);
                        break;
                }
            });
        }
    }
    private async Task<Guid> CaseAsync(SeededMember office, Guid? lawyer = null)
    {
        var client = await _api.CreateAsync("/api/v1/people", office.UserId, office.OrganizationId, new { kind = "company", name = "Cliente", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", office.UserId, office.OrganizationId, new
        {
            kind = "judicial",
            cnjNumber = "2238058-98.2025.8.26.8434",
            instance = "first",
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = lawyer ?? office.UserId } },
        });
    }

    [Fact]
    public async Task Capturing_twice_does_not_duplicate_publications_and_every_item_is_linked_or_waits_for_linking()
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var source = await SourceAsync(office);
        await CaptureAsync(office, source);
        await CaptureAsync(office, source);
        await CaptureAsync(office, source, force: true);
        var (_, page) = await SendAsync(office, HttpMethod.Get, "/api/v1/publications");
        page.GetProperty("items").GetArrayLength().ShouldBe(2);
        page.GetProperty("items").EnumerateArray().Count(p => p.GetProperty("legalCaseId").ValueKind == JsonValueKind.Null).ShouldBe(1);
        page.GetProperty("items").EnumerateArray().Count(p => p.GetProperty("legalCaseId").ValueKind == JsonValueKind.String && p.GetProperty("legalCaseId").GetGuid() == caseId).ShouldBe(1);
        var (_, state) = await SendAsync(office, HttpMethod.Get, $"/api/v1/publication-sources/{source}/capture");
        state.GetProperty("runs").GetArrayLength().ShouldBe(2);
        state.GetProperty("runs")[0].GetProperty("status").GetString().ShouldBe("succeeded");
        _factory.PublishedEvents.OfType<PublicationCapturedIntegrationEvent>().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Concurrent_captures_and_concurrent_intake_keep_one_row_per_hash()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => CaptureAsync(office, source, true)));
        var ids = new ConcurrentBag<Guid>();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => OfficeScopeAsync(office, async sp =>
        {
            var result = await sp.GetRequiredService<ICommandHandler<ReceivePublicationCommand, Guid>>()
                .Handle(new(new(source, "DJE", _clock.Today, _clock.Today, "Mesmo texto")), Ct);
            result.IsSuccess.ShouldBeTrue();
            ids.Add(result.Value);
        })));
        ids.Distinct().Count().ShouldBe(1);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications")).Body.GetProperty("items").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task An_unavailable_provider_raises_one_missing_capture_alert_and_recovery_resolves_it()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        _options.Available = false;
        await CaptureAsync(office, source);
        await CaptureAsync(office, source);
        await MonitorAsync(office, source);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/capture-alerts")).Body.GetArrayLength().ShouldBe(0);
        var measured = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) => { if (string.Equals(instrument.Meter.Name, CaptureMetrics.MeterName, StringComparison.Ordinal)) { meter.EnableMeasurementEvents(instrument); } };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref measured, value));
        listener.Start();
        _clock.Advance(TimeSpan.FromHours(2));
        await MonitorAsync(office, source);
        await MonitorAsync(office, source);
        measured.ShouldBe(1);
        var (_, alerts) = await SendAsync(office, HttpMethod.Get, "/api/v1/publications/capture-alerts");
        alerts.GetArrayLength().ShouldBe(1);
        alerts[0].GetProperty("errorCode").GetString().ShouldBe("publications.provider_unavailable");
        _options.Available = true;
        _clock.Advance(TimeSpan.FromMinutes(10));
        await CaptureAsync(office, source, true);
        await MonitorAsync(office, source);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/capture-alerts")).Body[0].GetProperty("resolvedAt").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Empty_success_is_complete_and_late_activation_gets_one_hour()
    {
        var office = await OfficeAsync();
        _options.Empty = true;
        var source = await SourceAsync(office);
        await CaptureAsync(office, source);
        _clock.Advance(TimeSpan.FromHours(2));
        await MonitorAsync(office, source);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/capture-alerts")).Body.GetArrayLength().ShouldBe(0);
        _clock.Advance(TimeSpan.FromHours(1));
        var late = await SourceAsync(office);
        _options.Available = false;
        await CaptureAsync(office, late);
        _clock.Advance(TimeSpan.FromMinutes(59));
        await MonitorAsync(office, late);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/capture-alerts")).Body.GetArrayLength().ShouldBe(0);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await MonitorAsync(office, late);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/capture-alerts")).Body.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Missing_capture_email_goes_only_to_office_integrators_and_controllers()
    {
        var office = await OfficeAsync();
        async Task<string> AddAsync(DataScope scope, string[] permissions)
        {
            var address = TestAccounts.NewEmail();
            var account = await TestAccounts.CreateAsync(dependencies, address);
            await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, account.Id, scope, null, permissions);
            return address;
        }
        var controller = await AddAsync(DataScope.Organization, Controllers);
        var integrator = await AddAsync(DataScope.Organization, Integrators);
        var scoped = await AddAsync(DataScope.Own, Controllers);
        var reader = await AddAsync(DataScope.Organization, Readers);
        var source = await SourceAsync(office);
        _clock.Advance(TimeSpan.FromHours(2));
        await MonitorAsync(office, source);
        await ConsumeEventsAsync(office);
        var sent = _mail.Messages.SelectMany(m => m.To).ToList();
        sent.ShouldContain(controller);
        sent.ShouldContain(integrator);
        sent.ShouldNotContain(scoped);
        sent.ShouldNotContain(reader);
        sent.Count.ShouldBe(2);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/notifications")).Body.GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task New_publication_notifications_follow_current_case_access_and_reads_are_per_user()
    {
        var office = await OfficeAsync();
        var lawyer = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null, Readers);
        var caseId = await CaseAsync(office, lawyer.UserId);
        var source = await SourceAsync(office);
        await CaptureAsync(office, source);
        await ConsumeEventsAsync(office);
        _mail.Messages.ShouldBeEmpty();
        var (_, all) = await SendAsync(office, HttpMethod.Get, "/api/v1/notifications");
        all.GetProperty("items").GetArrayLength().ShouldBe(2);
        var (_, own) = await SendAsync(lawyer, HttpMethod.Get, "/api/v1/notifications");
        own.GetProperty("items").GetArrayLength().ShouldBe(1);
        var notification = own.GetProperty("items")[0].GetProperty("id").GetGuid();
        (await SendAsync(lawyer, HttpMethod.Post, $"/api/v1/notifications/{notification}/read")).Status.ShouldBe(HttpStatusCode.NoContent);
        (await SendAsync(lawyer, HttpMethod.Post, $"/api/v1/notifications/{notification}/read")).Status.ShouldBe(HttpStatusCode.NoContent);
        (await SendAsync(lawyer, HttpMethod.Get, "/api/v1/notifications?unreadOnly=true")).Body.GetProperty("items").GetArrayLength().ShouldBe(0);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/notifications?unreadOnly=true")).Body.GetProperty("items").GetArrayLength().ShouldBe(2);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions", new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = lawyer.UserId } } })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(lawyer, HttpMethod.Get, "/api/v1/notifications")).Body.GetProperty("items").GetArrayLength().ShouldBe(0);
        (await SendAsync(lawyer, HttpMethod.Post, $"/api/v1/notifications/{notification}/read")).Status.ShouldBe(HttpStatusCode.NotFound);
        var other = await OfficeAsync();
        (await SendAsync(other, HttpMethod.Post, $"/api/v1/notifications/{notification}/read")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(other, HttpMethod.Get, "/api/v1/notifications")).Body.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Reenabled_sources_capture_immediately_and_inactive_sources_are_not_monitored()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office, false);
        await CaptureAsync(office, source, true);
        await MonitorAsync(office, source);
        _clock.Advance(TimeSpan.FromHours(3));
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{source}", new { kind = "oab", label = "Fonte", value = "SP 123456", isActive = true })).Status.ShouldBe(HttpStatusCode.OK);
        await ConsumeEventsAsync(office);
        await _factory.RunJobsAsync(office.OrganizationId);
        var (_, state) = await SendAsync(office, HttpMethod.Get, $"/api/v1/publication-sources/{source}/capture");
        state.GetProperty("runs").GetArrayLength().ShouldBe(1);
        state.GetProperty("runs")[0].GetProperty("status").GetString().ShouldBe("succeeded");
        await CaptureAsync(office, Guid.CreateVersion7());
    }

    [Fact]
    public async Task SignalR_stream_recovers_persisted_notifications_and_revalidates_case_access()
    {
        var office = await OfficeAsync();
        var lawyer = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null, Readers);
        var caseId = await CaseAsync(office, lawyer.UserId);
        var source = await SourceAsync(office);
        await using var connected = new SignalRNotificationProbe(_factory.CreateClient());
        await connected.ConnectAsync(lawyer, Ct);
        (await connected.NextAsync(Ct)).GetProperty("items").GetArrayLength().ShouldBe(0);
        await CaptureAsync(office, source);
        await ConsumeEventsAsync(office);
        var delivered = await connected.NextAsync(Ct);
        delivered.GetProperty("items").GetArrayLength().ShouldBe(1);
        await using var reconnected = new SignalRNotificationProbe(_factory.CreateClient());
        await reconnected.ConnectAsync(lawyer, Ct);
        var restored = await reconnected.NextAsync(Ct);
        restored.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(delivered.GetProperty("items")[0].GetProperty("id").GetGuid());
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions", new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = lawyer.UserId } } })).Status.ShouldBe(HttpStatusCode.OK);
        (await connected.NextAsync(Ct)).GetProperty("items").GetArrayLength().ShouldBe(0);
        (await reconnected.NextAsync(Ct)).GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Notification_pagination_skips_invisible_rows_and_validates_cursors()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        await CaptureAsync(office, source);
        await ConsumeEventsAsync(office);
        await OfficeScopeAsync(office, async sp =>
        {
            var db = sp.GetRequiredService<PlatformDbContext>();
            for (var i = 0; i < 205; i++)
            {
                db.Notifications.Add(Notification.Create("unknown:" + i, "unknown.kind", Guid.CreateVersion7(), null, _clock.UtcNow.AddHours(1)));
            }
            await db.SaveChangesAsync(Ct);
        });
        var (_, first) = await SendAsync(office, HttpMethod.Get, "/api/v1/notifications?limit=1");
        first.GetProperty("items").GetArrayLength().ShouldBe(1);
        var (_, second) = await SendAsync(office, HttpMethod.Get, "/api/v1/notifications?limit=1&cursor=" + first.GetProperty("nextCursor").GetString());
        second.GetProperty("items").GetArrayLength().ShouldBe(1);
        second.GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/notifications?cursor=invalid")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/notifications?limit=0")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/capture-alerts?limit=201")).Status.ShouldBe(HttpStatusCode.BadRequest);
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), Readers);
        (await SendAsync(reader, HttpMethod.Get, $"/api/v1/publication-sources/{source}/capture")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/publication-sources/{Guid.CreateVersion7()}/capture")).Status.ShouldBe(HttpStatusCode.NotFound);
    }
}

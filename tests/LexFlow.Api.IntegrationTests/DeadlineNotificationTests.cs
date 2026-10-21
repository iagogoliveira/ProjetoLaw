using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Platform.Infrastructure.Notifications;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class DeadlineNotificationTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly string[] OwnerPermissions =
    [
        "cases.create", "cases.edit", "cases.read", "cases.restrictions.manage", "crm.people.create",
        "schedule.deadlines.read", "schedule.deadlines.verify",
    ];
    private static readonly string[] ClientRole = ["client"];
    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
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
    public async Task Missed_deadline_notification_is_idempotent_and_visible_to_only_authorized_recipients()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, OwnerPermissions);
        var assignee = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "cases.read", "schedule.deadlines.read");
        var reviewer = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            "cases.read", "schedule.deadlines.read");
        var verifier = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            "cases.read", "schedule.deadlines.read", "schedule.deadlines.verify");
        var ordinaryReader = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "cases.read", "schedule.deadlines.read");
        var otherOffice = await TestOrganizations.CreateWithMemberAsync(dependencies, "cases.read", "schedule.deadlines.read");
        var caseId = await CreateCaseAsync(office, assignee.UserId);
        var (deadlineId, activityId) = await CreateMissedDeadlineAsync(office, caseId, assignee.UserId, reviewer.UserId);
        var integrationEvent = Event(office.OrganizationId, activityId, deadlineId, caseId, "scheduling.deadline_missed");

        await ConsumeAsync(integrationEvent);
        await ConsumeAsync(integrationEvent);

        var assigneeNotifications = await NotificationsAsync(assignee);
        var reviewerNotifications = await NotificationsAsync(reviewer);
        var verifierNotifications = await NotificationsAsync(verifier);
        var ordinaryNotifications = await NotificationsAsync(ordinaryReader);
        var otherOfficeNotifications = await NotificationsAsync(otherOffice);

        assigneeNotifications.GetProperty("items").GetArrayLength().ShouldBe(1);
        reviewerNotifications.GetProperty("items").GetArrayLength().ShouldBe(1);
        verifierNotifications.GetProperty("items").GetArrayLength().ShouldBe(1);
        ordinaryNotifications.GetProperty("items").GetArrayLength().ShouldBe(0);
        otherOfficeNotifications.GetProperty("items").GetArrayLength().ShouldBe(0);

        var notification = assigneeNotifications.GetProperty("items")[0];
        notification.GetProperty("kind").GetString().ShouldBe("scheduling.deadline_missed");
        notification.GetProperty("resourceId").GetGuid().ShouldBe(activityId);
        (await SendAsync(assignee, HttpMethod.Post, $"/api/v1/notifications/{notification.GetProperty("id").GetGuid()}/read")).Status
            .ShouldBe(HttpStatusCode.NoContent);
        (await NotificationsAsync(assignee, "/api/v1/notifications?unreadOnly=true")).GetProperty("items").GetArrayLength().ShouldBe(0);
        (await NotificationsAsync(reviewer, "/api/v1/notifications?unreadOnly=true")).GetProperty("items").GetArrayLength().ShouldBe(1);

        var restriction = await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions", new
        {
            allowed = Array.Empty<object>(),
            blocked = new[] { new { userId = assignee.UserId } },
        });
        restriction.Status.ShouldBe(HttpStatusCode.OK, restriction.Body.ToString());
        (await NotificationsAsync(assignee)).GetProperty("items").GetArrayLength().ShouldBe(0);
        (await SendAsync(assignee, HttpMethod.Post, $"/api/v1/notifications/{notification.GetProperty("id").GetGuid()}/read")).Status
            .ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_event_kind_and_missing_activity_do_not_create_notifications()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, OwnerPermissions);
        var caseId = await CreateCaseAsync(office, office.UserId);
        var (deadlineId, activityId) = await CreateMissedDeadlineAsync(office, caseId, office.UserId, null);

        await ConsumeAsync(Event(office.OrganizationId, activityId, deadlineId, caseId, "scheduling.deadline_completed"));
        await ConsumeAsync(Event(office.OrganizationId, Guid.CreateVersion7(), Guid.CreateVersion7(), caseId, "scheduling.deadline_missed"));

        (await NotificationsAsync(office)).GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember user, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, user.UserId, user.OrganizationId, body);

    [Fact]
    public async Task Public_consumer_records_the_missed_metric_once_after_persisting_the_notification()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, OwnerPermissions);
        var caseId = await CreateCaseAsync(office, office.UserId);
        var (deadlineId, activityId) = await CreateMissedDeadlineAsync(office, caseId, office.UserId, null);
        var integrationEvent = Event(office.OrganizationId, activityId, deadlineId, caseId, "scheduling.deadline_missed");
        long measurements = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (string.Equals(instrument.Meter.Name, "LexFlow.Scheduling", StringComparison.Ordinal)
                && string.Equals(instrument.Name, "lexflow_deadlines_missed_total", StringComparison.Ordinal))
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref measurements, value));
        listener.Start();

        await ConsumeAsync(integrationEvent, publicHandler: true);
        await ConsumeAsync(integrationEvent, publicHandler: true);

        measurements.ShouldBe(1);
        (await NotificationsAsync(office)).GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    private async Task<JsonElement> NotificationsAsync(SeededMember user, string path = "/api/v1/notifications")
    {
        var (status, body) = await SendAsync(user, HttpMethod.Get, path);
        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        return body;
    }

    private async Task<Guid> CreateCaseAsync(SeededMember office, Guid lawyerUserId)
    {
        var clientId = await _api.CreateAsync("/api/v1/people", office.UserId, office.OrganizationId,
            new { kind = "company", name = $"Cliente {Guid.CreateVersion7():N}", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", office.UserId, office.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = clientId, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = lawyerUserId } },
        });
    }

    private async Task<(Guid DeadlineId, Guid ActivityId)> CreateMissedDeadlineAsync(
        SeededMember office,
        Guid caseId,
        Guid assigneeUserId,
        Guid? reviewerUserId)
    {
        using var ambient = AmbientContext.Begin(office.OrganizationId, "deadline-notification-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office.OrganizationId);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var deadline = Deadline.Create(caseId, Guid.CreateVersion7(), "Deadline notification test",
            new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 25), new DateOnly(2026, 11, 2),
            assigneeUserId, true, office.UserId, clock.UtcNow).Value;
        if (reviewerUserId is { } reviewer)
        {
            deadline.Review(reviewer, singleMember: false, clock.UtcNow).IsSuccess.ShouldBeTrue();
        }

        deadline.MarkMissed("Missed during integration test", office.UserId, clock.UtcNow).IsSuccess.ShouldBeTrue();
        var activity = DeadlineActivity.Record(deadline, "scheduling.deadline_missed", office.UserId, clock.UtcNow);
        db.Deadlines.Add(deadline);
        db.DeadlineActivities.Add(activity);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (deadline.Id, activity.Id);
    }

    private async Task ConsumeAsync(DeadlineAlertIntegrationEvent integrationEvent, bool publicHandler = false)
    {
        using var ambient = AmbientContext.Begin(integrationEvent.OrganizationId, "deadline-notification-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(integrationEvent.OrganizationId);
        var consumer = ActivatorUtilities.CreateInstance<DeadlineNotificationConsumer>(scope.ServiceProvider);
        if (publicHandler) { await consumer.Handle(integrationEvent, TestContext.Current.CancellationToken); }
        else { await ((IIntegrationEventHandler<DeadlineAlertIntegrationEvent>)consumer).Handle(integrationEvent, TestContext.Current.CancellationToken); }
    }

    private static DeadlineAlertIntegrationEvent Event(Guid organizationId, Guid activityId, Guid deadlineId, Guid caseId, string kind) =>
        new(activityId, deadlineId, caseId, kind)
        {
            EventId = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            OccurredAt = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
        };
}

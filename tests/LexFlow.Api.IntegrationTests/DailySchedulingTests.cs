using System.Collections.Concurrent;
using System.Net;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Infrastructure.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class DailySchedulingTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string[] Permissions = ["cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create", "schedule.tasks.read", "schedule.tasks.manage", "schedule.deadlines.read"];
    private static readonly string[] ClientRole = ["client"];
    private readonly DigestClock _clock = new();
    private readonly RecordingMail _mail = new();
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(dependencies.Settings, replaceServices: services =>
        {
            services.AddSingleton<IClock>(_clock);
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

    [Fact]
    public async Task Daily_jobs_wait_until_local_0730_and_deliver_real_agenda_only_once()
    {
        var office = await CreateOfficeAsync();
        var taskTitle = "Prepare <script>alert('secret')</script>";
        var task = await _api.SendAsync(HttpMethod.Post, "/api/v1/tasks", office.UserId, office.OrganizationId,
            new { title = taskTitle, dueOn = "2026-10-21", assigneeUserId = office.UserId, priority = "normal" });
        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        await SetPreferenceAsync(office, enabled: true);

        await DispatchAsync();
        _factory.Jobs.TryDequeue(out _).ShouldBeFalse();
        await RunOfficeAsync(office);
        await _factory.RunJobsAsync(office.OrganizationId);
        _mail.Messages.ShouldBeEmpty();
        (await DeliveriesAsync(office)).ShouldBe(0);

        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        await RunOfficeAsync(office);
        await _factory.RunJobsAsync(office.OrganizationId);
        var message = _mail.Messages.Single();
        message.TextBody.ShouldNotBeNull();
        message.TextBody!.ShouldContain(taskTitle);
        message.HtmlBody.ShouldContain("&lt;script&gt;");
        message.HtmlBody.ShouldNotContain("<script>");
        (await DeliveriesAsync(office)).ShouldBe(1);

        await RunOfficeAsync(office);
        await _factory.RunJobsAsync(office.OrganizationId);
        _mail.Messages.Count.ShouldBe(1);
        (await DeliveriesAsync(office)).ShouldBe(1);
    }

    [Fact]
    public async Task Fatal_due_today_reminder_is_created_once_for_real_deadline()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        var office = await CreateOfficeAsync();
        var verifier = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            "cases.read", "schedule.deadlines.read", "schedule.deadlines.verify");
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            "cases.read", "schedule.deadlines.read");
        var client = await _api.CreateAsync("/api/v1/people", office.UserId, office.OrganizationId,
            new { kind = "company", name = $"Client {Guid.CreateVersion7():N}", roles = ClientRole });
        var caseId = await _api.CreateAsync("/api/v1/cases", office.UserId, office.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = office.UserId } },
        });
        Guid deadlineId;
        using (var ambient = AmbientContext.Begin(office.OrganizationId, "daily-scheduling-tests"))
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office.OrganizationId);
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var deadline = Deadline.Create(caseId, Guid.CreateVersion7(), "Fatal today",
                new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21),
                office.UserId, true, office.UserId, _clock.UtcNow).Value;
            deadlineId = deadline.Id;
            db.Deadlines.Add(deadline);
            await db.SaveChangesAsync(Ct);
        }

        await RunOfficeAsync(office);
        await RunOfficeAsync(office);
        using var notificationAmbient = AmbientContext.Begin(office.OrganizationId, "daily-scheduling-tests");
        await using var notificationScope = _factory.Services.CreateAsyncScope();
        using var notificationTenant = notificationScope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office.OrganizationId);
        var notifications = notificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>().Notifications;
        (await notifications.CountAsync(n => n.Kind == "scheduling.deadline_due_today" && n.ResourceId == deadlineId, Ct)).ShouldBe(1);
        (await NotificationCountAsync(office)).ShouldBe(1);
        (await NotificationCountAsync(verifier)).ShouldBe(1);
        (await NotificationCountAsync(reader)).ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_digest_jobs_send_only_one_email()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        var office = await CreateOfficeAsync();
        var task = await _api.SendAsync(HttpMethod.Post, "/api/v1/tasks", office.UserId, office.OrganizationId,
            new { title = "Concurrent digest", dueOn = "2026-10-21", assigneeUserId = office.UserId, priority = "normal" });
        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        await SetPreferenceAsync(office, enabled: true);

        await Task.WhenAll(RunDigestAsync(office), RunDigestAsync(office));
        _mail.Messages.Count.ShouldBe(1);
        (await DeliveriesAsync(office)).ShouldBe(1);
    }

    [Fact]
    public async Task Restricted_case_is_removed_from_digest_before_sending()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        var owner = await CreateOfficeAsync();
        var recipientAccount = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail());
        var recipient = await TestOrganizations.AddMemberAsync(dependencies, owner.OrganizationId, recipientAccount.Id,
            "cases.read", "schedule.deadlines.read", "profile.manage");
        var client = await _api.CreateAsync("/api/v1/people", owner.UserId, owner.OrganizationId,
            new { kind = "company", name = $"Client {Guid.CreateVersion7():N}", roles = ClientRole });
        var caseId = await _api.CreateAsync("/api/v1/cases", owner.UserId, owner.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = recipient.UserId } },
        });
        using (var ambient = AmbientContext.Begin(owner.OrganizationId, "daily-scheduling-tests"))
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(owner.OrganizationId);
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            db.Deadlines.Add(Deadline.Create(caseId, Guid.CreateVersion7(), "Confidential deadline",
                new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22),
                recipient.UserId, true, owner.UserId, _clock.UtcNow).Value);
            await db.SaveChangesAsync(Ct);
        }
        await SetPreferenceAsync(recipient, enabled: true);
        var restricted = await _api.SendAsync(HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions", owner.UserId, owner.OrganizationId,
            new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = recipient.UserId } } });
        restricted.Status.ShouldBe(HttpStatusCode.OK, restricted.Body.ToString());

        await RunDigestAsync(recipient);
        _mail.Messages.ShouldBeEmpty();
        (await DeliveriesAsync(recipient)).ShouldBe(0);
    }

    [Fact]
    public async Task Failed_email_does_not_mark_delivery_and_can_be_retried()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        var office = await CreateOfficeAsync();
        var task = await _api.SendAsync(HttpMethod.Post, "/api/v1/tasks", office.UserId, office.OrganizationId,
            new { title = "Retry digest", dueOn = "2026-10-21", assigneeUserId = office.UserId, priority = "normal" });
        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        await SetPreferenceAsync(office, enabled: true);

        _mail.FailNext = true;
        await Should.ThrowAsync<InvalidOperationException>(() => RunDigestAsync(office));
        (await DeliveriesAsync(office)).ShouldBe(0);

        await RunDigestAsync(office);
        _mail.Messages.Count.ShouldBe(1);
        (await DeliveriesAsync(office)).ShouldBe(1);
        await RunDigestAsync(office);
        _mail.Messages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Opt_out_and_section_preferences_are_respected_at_delivery_time()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        var office = await CreateOfficeAsync();
        var task = await _api.SendAsync(HttpMethod.Post, "/api/v1/tasks", office.UserId, office.OrganizationId,
            new { title = "Hidden agenda", dueOn = "2026-10-21", assigneeUserId = office.UserId, priority = "normal" });
        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());

        await SetPreferenceAsync(office, enabled: false);
        await RunDigestAsync(office);
        _mail.Messages.ShouldBeEmpty();

        await SetPreferenceAsync(office, enabled: true, agenda: false);
        await RunDigestAsync(office);
        _mail.Messages.ShouldBeEmpty();
        (await DeliveriesAsync(office)).ShouldBe(0);
    }

    [Fact]
    public async Task Unconfirmed_account_is_not_sent_a_digest()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 30, 0, TimeSpan.Zero);
        var office = await CreateOfficeAsync(confirmed: false);
        var task = await _api.SendAsync(HttpMethod.Post, "/api/v1/tasks", office.UserId, office.OrganizationId,
            new { title = "Unconfirmed digest", dueOn = "2026-10-21", assigneeUserId = office.UserId, priority = "normal" });
        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        await SetPreferenceAsync(office, enabled: true);

        await RunDigestAsync(office);
        _mail.Messages.ShouldBeEmpty();
        (await DeliveriesAsync(office)).ShouldBe(0);
    }

    private async Task<SeededMember> CreateOfficeAsync(bool confirmed = true)
    {
        var user = await TestAccounts.CreateAsync(dependencies, TestAccounts.NewEmail(), emailConfirmed: confirmed);
        var initial = await TestOrganizations.CreateWithMemberAsync(dependencies, Permissions);
        return await TestOrganizations.AddMemberAsync(dependencies, initial.OrganizationId, user.Id, Permissions);
    }

    private async Task SetPreferenceAsync(SeededMember office, bool enabled, bool agenda = true)
    {
        var result = await _api.SendAsync(HttpMethod.Put, "/api/v1/me/notification-settings", office.UserId, office.OrganizationId,
            new { dailyDigestEnabled = enabled, includeAgenda = agenda, includeDeadlines = true, includePublications = true, mutedKinds = Array.Empty<string>() });
        result.Status.ShouldBe(HttpStatusCode.OK, result.Body.ToString());
    }

    private async Task DispatchAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DailySchedulingDispatchJob>().RunAsync();
    }

    private async Task RunOfficeAsync(SeededMember office)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DailyOfficeNotificationsJob>().RunAsync(office.OrganizationId, new DateOnly(2026, 10, 21));
    }

    private async Task RunDigestAsync(SeededMember office)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DailyDigestJob>().RunAsync(office.OrganizationId, office.UserId, new DateOnly(2026, 10, 21));
    }

    private async Task<int> DeliveriesAsync(SeededMember office)
    {
        using var ambient = AmbientContext.Begin(office.OrganizationId, "daily-scheduling-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office.OrganizationId);
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().DigestDeliveries
            .CountAsync(d => d.UserId == office.UserId && d.SentAt != null, Ct);
    }

    private async Task<int> NotificationCountAsync(SeededMember member)
    {
        var result = await _api.SendAsync(HttpMethod.Get, "/api/v1/notifications", member.UserId, member.OrganizationId);
        result.Status.ShouldBe(HttpStatusCode.OK, result.Body.ToString());
        return result.Body.GetProperty("items").GetArrayLength();
    }

    private sealed class DigestClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 21, 10, 29, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, LegalTimeZone).DateTime);
    }

    private sealed class RecordingMail : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Messages { get; } = new();
        public bool FailNext { get; set; }
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (FailNext) { FailNext = false; throw new InvalidOperationException("Transient email failure."); }
            Messages.Enqueue(message);
            return Task.CompletedTask;
        }
    }
}

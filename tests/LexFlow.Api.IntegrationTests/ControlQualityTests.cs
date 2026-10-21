using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Organization.Infrastructure.Persistence;
using LexFlow.Publications.Application;
using LexFlow.Publications.Application.Triage;
using LexFlow.Scheduling.Application;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed class ControlQualityTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string[] AdminPermissions =
    [
        "settings.integrations.manage", "settings.users.manage", "publications.read", "publications.triage",
        "publications.distribute", "publications.close", "cases.create", "cases.read", "cases.edit",
        "cases.restrictions.manage", "crm.people.create", "schedule.control.read", "schedule.deadlines.read",
        "schedule.deadlines.manage", "schedule.tasks.read", "schedule.tasks.manage",
    ];
    private static readonly string[] ClientRole = ["client"];
    private readonly QualityClock _clock = new();
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(dependencies.Settings, replaceServices: services => services.AddSingleton<IClock>(_clock));
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
    public async Task Publication_counts_metrics_and_limit_use_the_authorized_window()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, AdminPermissions);
        var source = await SourceAsync(office);
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        await ReceiveAsync(office, source, "First pending publication");
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero);
        var triaged = await ReceiveAsync(office, source, "Publication triaged once");
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 11, 15, 0, TimeSpan.Zero);
        var triage = await SendAsync(office, HttpMethod.Post, $"/api/v1/publications/{triaged}/triage", Discard("Triagem confirmada"));
        triage.Status.ShouldBe(HttpStatusCode.OK, triage.Body.ToString());
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        await ReceiveAsync(office, source, "Second pending publication");

        var (status, report) = await ReportAsync(office, "&limit=1");

        status.ShouldBe(HttpStatusCode.OK, report.ToString());
        var pending = Section(report, "pendingPublications");
        pending.GetProperty("count").GetInt64().ShouldBe(2);
        pending.GetProperty("items").GetArrayLength().ShouldBe(1);
        var average = Metric(report, "averageTriageMinutes");
        average.GetProperty("sampleSize").GetInt64().ShouldBe(1);
        average.GetProperty("value").GetDecimal().ShouldBe(15m);
        var retriaged = Metric(report, "retriagedPublications");
        retriaged.GetProperty("value").GetDecimal().ShouldBe(0m);
    }

    [Fact]
    public async Task Deadline_sections_exclude_completed_protocols_and_count_inactive_assignees()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, AdminPermissions);
        var assignee = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            "cases.read", "schedule.deadlines.read");
        var caseId = await CaseAsync(office, assignee.UserId);
        var source = await SourceAsync(office);
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        var openDeadline = await CreateDeadlineFromPublicationAsync(office, source, caseId, assignee.UserId, "Open overdue deadline");
        var completedDeadline = await CreateDeadlineFromPublicationAsync(office, source, caseId, office.UserId, "Completed deadline");
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/deadlines/{completedDeadline}/start")).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/deadlines/{completedDeadline}/complete", new { evidence = "Protocolo 123" })).Status.ShouldBe(HttpStatusCode.OK);
        await DeactivateMemberAsync(office.OrganizationId, assignee.MemberId);

        var (status, report) = await ReportAsync(office);

        status.ShouldBe(HttpStatusCode.OK, report.ToString());
        var unassigned = Section(report, "unassignedDeadlines");
        unassigned.GetProperty("count").GetInt64().ShouldBe(1);
        unassigned.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(openDeadline);
        var withoutEvidence = Section(report, "deadlinesWithoutEvidence");
        withoutEvidence.GetProperty("count").GetInt64().ShouldBe(1);
        withoutEvidence.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(openDeadline);
        var late = Metric(report, "lateRegisteredDeadlines");
        late.GetProperty("value").GetDecimal().ShouldBe(2m);
        late.GetProperty("sampleSize").GetInt64().ShouldBe(2);
    }

    [Fact]
    public async Task Publication_counts_and_metrics_respect_case_scope_restrictions_and_tenant()
    {
        var office = await TestOrganizations.CreateWithMemberAsync(dependencies, AdminPermissions);
        var reader = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            DataScope.Own, null, "cases.read", "publications.read", "schedule.control.read");
        const string visibleCnj = "2238058-98.2025.8.26.8434";
        const string secretCnj = "6616920-53.2022.4.01.1974";
        var visibleCase = await CaseAsync(office, reader.UserId, visibleCnj);
        var secretCase = await CaseAsync(office, office.UserId, secretCnj);
        var source = await SourceAsync(office);
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        await ReceiveAsync(office, source, "Visible linked publication " + visibleCnj);
        var visibleTriage = await ReceiveAsync(office, source, "Visible triaged publication " + visibleCnj);
        var secretTriage = await ReceiveAsync(office, source, "Secret triaged publication " + secretCnj);
        var otherOffice = await TestOrganizations.CreateWithMemberAsync(dependencies, AdminPermissions);
        await ReceiveAsync(otherOffice, await SourceAsync(otherOffice), "Other tenant pending publication");
        _clock.UtcNow = new DateTimeOffset(2026, 10, 21, 10, 10, 0, TimeSpan.Zero);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/publications/{visibleTriage}/triage", Discard("Visible"))).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/publications/{secretTriage}/triage", Discard("Secret"))).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{secretCase}/restrictions", new
        {
            allowed = Array.Empty<object>(),
            blocked = new[] { new { userId = reader.UserId } },
        })).Status.ShouldBe(HttpStatusCode.OK);

        var (status, report) = await ReportAsync(reader);

        status.ShouldBe(HttpStatusCode.OK, report.ToString());
        var pending = Section(report, "pendingPublications");
        pending.GetProperty("count").GetInt64().ShouldBe(1);
        pending.ToString().ShouldNotContain(secretCase.ToString());
        pending.ToString().ShouldContain(visibleCase.ToString());
        var average = Metric(report, "averageTriageMinutes");
        average.GetProperty("sampleSize").GetInt64().ShouldBe(1);
        average.GetProperty("value").GetDecimal().ShouldBe(10m);
        average.ToString().ShouldNotContain(secretCase.ToString());
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    private Task<(HttpStatusCode Status, JsonElement Body)> ReportAsync(SeededMember member, string query = "") =>
        SendAsync(member, HttpMethod.Get, $"/api/v1/schedule/control?from=2026-10-21&until=2026-10-22{query}");

    private Task<Guid> SourceAsync(SeededMember member) => _api.CreateAsync("/api/v1/publication-sources", member.UserId, member.OrganizationId,
        new { kind = "name", label = "Escritório", value = "Controladoria" });

    private async Task<Guid> ReceiveAsync(SeededMember member, Guid sourceId, string text)
    {
        using var ambient = AmbientContext.Begin(member.OrganizationId, "control-quality-tests");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(member.OrganizationId);
        var command = new ReceivePublicationCommand(new(sourceId, "DJE", _clock.Today, _clock.Today, text));
        var result = await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReceivePublicationCommand, Guid>>().Handle(command, Ct);
        result.IsSuccess.ShouldBeTrue(result.Error?.Code);
        return result.Value;
    }

    private async Task DeactivateMemberAsync(Guid organizationId, Guid memberId)
    {
        using var ambient = AmbientContext.Begin(organizationId, "control-quality-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE org.members SET is_deleted = TRUE, deleted_at = {_clock.UtcNow} WHERE organization_id = {organizationId} AND id = {memberId}", Ct);
    }

    private async Task<Guid> CreateDeadlineFromPublicationAsync(SeededMember office, Guid source, Guid caseId, Guid assignee, string title)
    {
        var publication = await ReceiveAsync(office, source, $"{title} {Guid.CreateVersion7():N}");
        var (status, body) = await SendAsync(office, HttpMethod.Post, $"/api/v1/publications/{publication}/triage", new
        {
            classification = "deadlineNotice",
            action = "deadline",
            confirmed = true,
            caseId,
            deadline = new
            {
                title,
                startOn = new DateOnly(2026, 10, 18),
                internalDueOn = new DateOnly(2026, 10, 19),
                fatalDueOn = new DateOnly(2026, 10, 20),
                assigneeUserId = assignee,
            },
        });
        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        return body.GetProperty("triage").GetProperty("deadlineId").GetGuid();
    }

    private async Task<Guid> CaseAsync(SeededMember member, Guid lawyer, string? cnj = null)
    {
        var client = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId,
            new { kind = "company", name = $"Cliente {Guid.CreateVersion7():N}", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", member.UserId, member.OrganizationId, new
        {
            kind = cnj is null ? "extrajudicial" : "judicial",
            cnjNumber = cnj,
            instance = cnj is null ? null : "first",
            parties = new[] { new { personId = client, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = lawyer } },
        });
    }

    private static object Discard(string reason) => new { classification = "other", action = "discard", confirmed = true, reason };

    private static JsonElement Section(JsonElement report, string kind) => report.GetProperty("sections").EnumerateArray()
        .Single(section => section.GetProperty("kind").GetString() == kind);

    private static JsonElement Metric(JsonElement report, string kind) => report.GetProperty("metrics").EnumerateArray()
        .Single(metric => metric.GetProperty("kind").GetString() == kind);

    private sealed class QualityClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, LegalTimeZone).DateTime);
    }
}

using System.Globalization;
using System.Net;
using System.Text.Json;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class DeadlineLifecycleTests : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string[] CasePermissions = ["cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create", "schedule.deadlines.read", "schedule.deadlines.manage", "schedule.deadlines.verify", "schedule.calendar.manage", "settings.organization.manage"];
    private static readonly string[] ClientRole = ["client"];
    private readonly DependenciesFixture _dependencies;
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public DeadlineLifecycleTests(DependenciesFixture dependencies) => _dependencies = dependencies;

    public ValueTask InitializeAsync()
    {
        _factory = new LexFlowApiFactory(_dependencies.Settings);
        _http = _factory.CreateClient();
        _api = new OrganizationApi(_http);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _factory.DisposeAsync();
    }

    private sealed record Office(SeededMember Admin, Guid CaseId, Guid ClientId);
    private sealed record Calculation(string StartOn, string InternalDueOn, string FatalDueOn);

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    private async Task<Office> OfficeAsync(params string[] permissions)
    {
        var member = await TestOrganizations.CreateWithMemberAsync(_dependencies, permissions.Length == 0 ? CasePermissions : permissions);
        var clientId = await _api.CreateAsync("/api/v1/people", member.UserId, member.OrganizationId,
            new { kind = "company", name = "Cliente de Teste Ltda.", roles = ClientRole });
        var caseId = await _api.CreateAsync("/api/v1/cases", member.UserId, member.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = clientId, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = member.UserId } },
        });
        return new Office(member, caseId, clientId);
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> CalculateAsync(Office office, string triggerDate = "2027-03-01", int days = 5,
        int internalLeadDays = 1, Guid? courtId = null, string? stateCode = null, string? municipality = null, string? startKind = "publication") =>
        SendAsync(office.Admin, HttpMethod.Post, "/api/v1/deadlines/calculate", new
        {
            caseId = office.CaseId,
            courtId,
            stateCode,
            municipality,
            startKind,
            triggerDate,
            days,
            mode = "businessDays",
            internalLeadDays,
        });

    private async Task<Calculation> DatesAsync(Office office, string triggerDate = "2027-03-01", int days = 5,
        int internalLeadDays = 1, Guid? courtId = null, string? stateCode = null, string? municipality = null, string? startKind = "publication")
    {
        var (status, body) = await CalculateAsync(office, triggerDate, days, internalLeadDays, courtId, stateCode, municipality, startKind);
        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        return new Calculation(body.GetProperty("startsOn").GetString()!, body.GetProperty("internalDueOn").GetString()!, body.GetProperty("fatalDueOn").GetString()!);
    }

    private static object Request(SeededMember office, Calculation dates, bool confirmed = true, Guid? assignee = null,
        string triggerDate = "2027-03-01", int days = 5, int internalLeadDays = 1, string? stateCode = null, string? municipality = null) => new
        {
            type = "appeal",
            title = "Submit appeal",
            assigneeUserId = assignee ?? office.UserId,
            rule = new
            {
                startKind = "publication",
                triggerDate,
                consultedOn = (string?)null,
                days,
                mode = "businessDays",
                doubled = false,
                internalLeadDays,
            },
            confirmedInternalDueOn = dates.InternalDueOn,
            confirmedFatalDueOn = dates.FatalDueOn,
            confirmed,
            courtId = (Guid?)null,
            stateCode,
            municipality,
        };

    private Task<(HttpStatusCode Status, JsonElement Body)> CreateDeadlineAsync(Office office, object request) =>
        SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/cases/{office.CaseId}/deadlines", request);

    [Fact]
    public async Task Manual_deadlines_are_independent_without_publication_and_keep_confirmed_calculation()
    {
        var office = await OfficeAsync();
        var dates = await DatesAsync(office);

        var (firstStatus, first) = await CreateDeadlineAsync(office, Request(office.Admin, dates));
        var (secondStatus, second) = await CreateDeadlineAsync(office, Request(office.Admin, dates));
        var firstId = first.GetProperty("id").GetGuid();
        var (getStatus, detail) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/deadlines/{firstId}");

        firstStatus.ShouldBe(HttpStatusCode.Created, first.ToString());
        secondStatus.ShouldBe(HttpStatusCode.Created, second.ToString());
        firstId.ShouldNotBe(second.GetProperty("id").GetGuid());
        first.GetProperty("publicationId").ValueKind.ShouldBe(JsonValueKind.Null);
        first.GetProperty("startOn").GetString().ShouldBe(dates.StartOn);
        first.GetProperty("internalDueOn").GetString().ShouldBe(dates.InternalDueOn);
        first.GetProperty("fatalDueOn").GetString().ShouldBe(dates.FatalDueOn);
        first.GetProperty("confirmedBy").GetGuid().ShouldBe(office.Admin.UserId);
        first.GetProperty("confirmedAt").GetDateTimeOffset().Offset.ShouldBe(TimeSpan.Zero);
        first.GetProperty("calculation").ValueKind.ShouldBe(JsonValueKind.Object);
        first.GetProperty("rule").GetProperty("triggerDate").GetString().ShouldBe("2027-03-01");
        getStatus.ShouldBe(HttpStatusCode.OK, detail.ToString());
        detail.GetProperty("id").GetGuid().ShouldBe(firstId);
    }

    [Fact]
    public async Task Manual_creation_rejects_unconfirmed_or_stale_calculated_dates()
    {
        var office = await OfficeAsync();
        var dates = await DatesAsync(office);
        var unconfirmed = await CreateDeadlineAsync(office, Request(office.Admin, dates, confirmed: false));
        var stale = await CreateDeadlineAsync(office, new
        {
            type = "appeal",
            title = "Submit appeal",
            assigneeUserId = office.Admin.UserId,
            rule = new { startKind = "publication", triggerDate = "2027-03-01", consultedOn = (string?)null, days = 5, mode = "businessDays", doubled = false, internalLeadDays = 1 },
            confirmedInternalDueOn = DateOnly.ParseExact(dates.InternalDueOn, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(-1),
            confirmedFatalDueOn = dates.FatalDueOn,
            confirmed = true,
        });

        unconfirmed.Status.ShouldBe(HttpStatusCode.BadRequest, unconfirmed.Body.ToString());
        OrganizationApi.Code(unconfirmed.Body).ShouldBe("scheduling.deadline_invalid");
        stale.Status.ShouldBe(HttpStatusCode.Conflict, stale.Body.ToString());
        OrganizationApi.Code(stale.Body).ShouldBe("scheduling.calculation_changed");
    }

    [Fact]
    public async Task Deadline_transitions_require_evidence_and_reason_and_reject_repeated_terminal_actions()
    {
        var office = await OfficeAsync();
        var deadline = await RegisteredDeadlineAsync(office);
        var basePath = $"/api/v1/deadlines/{deadline}";

        var (startStatus, started) = await SendAsync(office.Admin, HttpMethod.Post, basePath + "/start");
        var (missingEvidenceStatus, missingEvidence) = await SendAsync(office.Admin, HttpMethod.Post, basePath + "/complete", new { evidence = " " });
        var (completeStatus, completed) = await SendAsync(office.Admin, HttpMethod.Post, basePath + "/complete", new { evidence = "  protocol 123  " });
        var repeated = await SendAsync(office.Admin, HttpMethod.Post, basePath + "/miss", new { reason = "too late" });

        startStatus.ShouldBe(HttpStatusCode.OK, started.ToString());
        started.GetProperty("status").GetString().ShouldBe("inProgress");
        missingEvidenceStatus.ShouldBe(HttpStatusCode.BadRequest, missingEvidence.ToString());
        OrganizationApi.Code(missingEvidence).ShouldBe("scheduling.evidence_required");
        completeStatus.ShouldBe(HttpStatusCode.OK, completed.ToString());
        completed.GetProperty("status").GetString().ShouldBe("completed");
        completed.GetProperty("completionEvidence").GetString().ShouldBe("protocol 123");
        completed.GetProperty("closedAt").GetDateTimeOffset().Offset.ShouldBe(TimeSpan.Zero);
        repeated.Status.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(repeated.Body).ShouldBe("scheduling.deadline_transition_invalid");

        var missedId = await RegisteredDeadlineAsync(office);
        var (missedStart, _) = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/deadlines/{missedId}/start");
        var (missingReasonStatus, missingReason) = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/deadlines/{missedId}/miss", new { reason = " " });
        var (missStatus, missed) = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/deadlines/{missedId}/miss", new { reason = "  Filing window elapsed  " });
        var repeatMiss = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/deadlines/{missedId}/miss", new { reason = "again" });

        missedStart.ShouldBe(HttpStatusCode.OK);
        missingReasonStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(missingReason).ShouldBe("scheduling.missed_reason_required");
        missStatus.ShouldBe(HttpStatusCode.OK, missed.ToString());
        missed.GetProperty("status").GetString().ShouldBe("missed");
        missed.GetProperty("missedReason").GetString().ShouldBe("Filing window elapsed");
        repeatMiss.Status.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Review_policy_allows_start_but_blocks_completion_until_review_and_uses_solo_exception()
    {
        var office = await OfficeAsync();
        var (_, policy) = await SendAsync(office.Admin, HttpMethod.Put, "/api/v1/organization/deadline-review", new { requireDeadlineReview = true });
        policy.GetProperty("requireDeadlineReview").GetBoolean().ShouldBeTrue();
        var deadline = await RegisteredDeadlineAsync(office);
        var path = $"/api/v1/deadlines/{deadline}";

        var (startStatus, started) = await SendAsync(office.Admin, HttpMethod.Post, path + "/start");
        var (blockedStatus, blocked) = await SendAsync(office.Admin, HttpMethod.Post, path + "/complete", new { evidence = "protocol" });
        var (reviewStatus, review) = await SendAsync(office.Admin, HttpMethod.Post, path + "/review");
        var (completeStatus, completed) = await SendAsync(office.Admin, HttpMethod.Post, path + "/complete", new { evidence = "protocol" });

        startStatus.ShouldBe(HttpStatusCode.OK, started.ToString());
        started.GetProperty("status").GetString().ShouldBe("inProgress");
        blockedStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(blocked).ShouldBe("scheduling.deadline_review_required");
        reviewStatus.ShouldBe(HttpStatusCode.OK, review.ToString());
        review.GetProperty("reviewedBy").GetGuid().ShouldBe(office.Admin.UserId);
        review.GetProperty("reviewedAt").GetDateTimeOffset().Offset.ShouldBe(TimeSpan.Zero);
        completeStatus.ShouldBe(HttpStatusCode.OK, completed.ToString());
    }

    private async Task<Guid> RegisteredDeadlineAsync(Office office)
    {
        var dates = await DatesAsync(office);
        var (status, body) = await CreateDeadlineAsync(office, Request(office.Admin, dates));
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        return body.GetProperty("id").GetGuid();
    }
}

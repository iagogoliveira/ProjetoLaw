using System.Globalization;
using System.Net;
using System.Text.Json;
using LexFlow.Organization.Domain.Roles;

namespace LexFlow.Api.IntegrationTests;

public sealed class SchedulingAgendaTests : IAsyncLifetime
{
    private static readonly string[] AdminPermissions =
    [
        "cases.create", "cases.read", "cases.edit", "cases.restrictions.manage", "crm.people.create",
        "schedule.deadlines.read", "schedule.deadlines.manage", "schedule.deadlines.verify",
        "schedule.tasks.read", "schedule.tasks.manage", "schedule.events.read", "schedule.events.manage",
        "settings.structure.manage",
    ];
    private static readonly string[] ClientRole = ["client"];
    private readonly DependenciesFixture _dependencies;
    private LexFlowApiFactory _factory = null!;
    private HttpClient _http = null!;
    private OrganizationApi _api = null!;

    public SchedulingAgendaTests(DependenciesFixture dependencies) => _dependencies = dependencies;

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

    private sealed record Office(SeededMember Admin, Guid CaseId);

    private Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(SeededMember member, HttpMethod method, string path, object? body = null) =>
        _api.SendAsync(method, path, member.UserId, member.OrganizationId, body);

    private async Task<Office> OfficeAsync()
    {
        var admin = await TestOrganizations.CreateWithMemberAsync(_dependencies, AdminPermissions);
        return new Office(admin, await CaseAsync(admin, admin.UserId));
    }

    private async Task<Guid> CaseAsync(SeededMember admin, Guid lawyer)
    {
        var clientId = await _api.CreateAsync("/api/v1/people", admin.UserId, admin.OrganizationId,
            new { kind = "company", name = $"Cliente {Guid.CreateVersion7():N}", roles = ClientRole });
        return await _api.CreateAsync("/api/v1/cases", admin.UserId, admin.OrganizationId, new
        {
            kind = "extrajudicial",
            parties = new[] { new { personId = clientId, pole = "active", isClient = true } },
            assignments = new[] { new { role = "lawyer", userId = lawyer } },
        });
    }

    private async Task<(Guid Id, DateOnly FatalDueOn)> CreateDeadlineAsync(Office office, Guid? caseId = null)
    {
        var (calculationStatus, calculation) = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/deadlines/calculate", new
        {
            caseId = caseId ?? office.CaseId,
            startKind = "publication",
            triggerDate = "2027-04-01",
            days = 5,
            mode = "businessDays",
            internalLeadDays = 1,
        });
        calculationStatus.ShouldBe(HttpStatusCode.OK, calculation.ToString());
        var fatalDueOn = DateOnly.ParseExact(calculation.GetProperty("fatalDueOn").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var (status, body) = await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/cases/{caseId ?? office.CaseId}/deadlines", new
        {
            type = "appeal",
            title = $"Prazo {Guid.CreateVersion7():N}",
            assigneeUserId = office.Admin.UserId,
            rule = new
            {
                startKind = "publication",
                triggerDate = "2027-04-01",
                consultedOn = (string?)null,
                days = 5,
                mode = "businessDays",
                doubled = false,
                internalLeadDays = 1,
            },
            confirmedInternalDueOn = calculation.GetProperty("internalDueOn").GetString(),
            confirmedFatalDueOn = calculation.GetProperty("fatalDueOn").GetString(),
            confirmed = true,
        });
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        return (body.GetProperty("id").GetGuid(), fatalDueOn);
    }

    private Task<(HttpStatusCode Status, JsonElement Body)> CreateTaskAsync(Office office, DateOnly dueOn, Guid? caseId = null, string title = "Tarefa") =>
        SendAsync(office.Admin, HttpMethod.Post, "/api/v1/tasks", new
        {
            caseId = caseId ?? office.CaseId,
            title,
            description = "Texto confidencial da tarefa",
            dueOn = dueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assigneeUserId = office.Admin.UserId,
            priority = "normal",
        });

    private Task<(HttpStatusCode Status, JsonElement Body)> CreateEventAsync(Office office, DateTimeOffset startsAt,
        DateTimeOffset endsAt, Guid? caseId = null, Guid? assignee = null, string title = "Evento") =>
        SendAsync(office.Admin, HttpMethod.Post, "/api/v1/schedule/events", new
        {
            caseId,
            title,
            description = "Descrição confidencial do compromisso",
            startsAt,
            endsAt,
            assigneeUserId = assignee ?? office.Admin.UserId,
            location = "Sala reservada",
        });

    private Task<(HttpStatusCode Status, JsonElement Body)> ScheduleHearingAsync(Office office, DateTimeOffset at,
        Guid? caseId = null, Guid? lawyer = null) =>
        SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/cases/{caseId ?? office.CaseId}/hearings", new
        {
            type = "conciliation",
            format = "virtual",
            scheduledAt = at,
            meetingLink = "https://meet.example.com/agenda",
            lawyerUserId = lawyer ?? office.Admin.UserId,
            representative = "Preposta confidencial",
            notes = "Notas confidenciais da audiência",
            witnessPersonIds = Array.Empty<Guid>(),
        });

    private Task<(HttpStatusCode Status, JsonElement Body)> AgendaAsync(SeededMember member, DateOnly from, DateOnly until,
        int? limit = null, string? cursor = null, bool? includeClosed = null, Guid? assignee = null, Guid? teamId = null)
    {
        var query = new List<string> { $"from={from:yyyy-MM-dd}", $"until={until:yyyy-MM-dd}" };
        if (limit is { } pageSize) { query.Add($"limit={pageSize}"); }
        if (cursor is not null) { query.Add($"cursor={Uri.EscapeDataString(cursor)}"); }
        if (includeClosed is { } closed) { query.Add($"includeClosed={closed.ToString().ToLowerInvariant()}"); }
        if (assignee is { } user) { query.Add($"assigneeUserId={user:D}"); }
        if (teamId is { } team) { query.Add($"teamId={team:D}"); }
        return SendAsync(member, HttpMethod.Get, $"/api/v1/schedule/agenda?{string.Join('&', query)}");
    }

    private static DateTimeOffset MidnightUtc(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), TimeSpan.FromHours(-3)).ToUniversalTime();

    [Fact]
    public async Task Agenda_merges_all_four_sources_and_cursor_pages_ties_without_duplicates_or_loss()
    {
        var office = await OfficeAsync();
        var deadline = await CreateDeadlineAsync(office);
        var day = deadline.FatalDueOn;
        var instant = MidnightUtc(day);
        var task = await CreateTaskAsync(office, day);
        var calendarEvent = await CreateEventAsync(office, instant, instant.AddHours(1), office.CaseId, title: "Compromisso");
        var hearing = await ScheduleHearingAsync(office, instant);
        task.Status.ShouldBe(HttpStatusCode.Created, task.Body.ToString());
        calendarEvent.Status.ShouldBe(HttpStatusCode.Created, calendarEvent.Body.ToString());
        hearing.Status.ShouldBe(HttpStatusCode.Created, hearing.Body.ToString());

        var expectedIds = new[]
        {
            deadline.Id,
            task.Body.GetProperty("id").GetGuid(),
            calendarEvent.Body.GetProperty("id").GetGuid(),
            hearing.Body.GetProperty("id").GetGuid(),
        };
        foreach (var pageSize in new[] { 1, 2 })
        {
            var ids = new List<Guid>();
            var kinds = new List<string>();
            string? cursor = null;
            do
            {
                var (status, body) = await AgendaAsync(office.Admin, day, day.AddDays(1), limit: pageSize, cursor: cursor);
                status.ShouldBe(HttpStatusCode.OK, body.ToString());
                foreach (var item in body.GetProperty("items").EnumerateArray())
                {
                    ids.Add(item.GetProperty("id").GetGuid());
                    kinds.Add(item.GetProperty("kind").GetString()!);
                    item.GetProperty("status").GetString().ShouldBe(item.GetProperty("kind").GetString() is "hearing" or "event" ? "scheduled" : "pending");
                    if (item.GetProperty("kind").GetString() == "deadline")
                    {
                        item.GetProperty("allDay").GetBoolean().ShouldBeTrue();
                        item.GetProperty("internalDueOn").GetString().ShouldNotBeNullOrWhiteSpace();
                    }
                    item.TryGetProperty("description", out _).ShouldBeFalse();
                    item.TryGetProperty("notes", out _).ShouldBeFalse();
                    item.TryGetProperty("witnesses", out _).ShouldBeFalse();
                }

                cursor = body.GetProperty("nextCursor").ValueKind == JsonValueKind.Null
                    ? null
                    : body.GetProperty("nextCursor").GetString();
            } while (cursor is not null);

            ids.Count.ShouldBe(4);
            ids.Distinct().Count().ShouldBe(4);
            ids.Order().ShouldBe(expectedIds.Order());
            kinds.ShouldBe(["deadline", "hearing", "event", "task"]);
        }
    }

    [Fact]
    public async Task Agenda_events_use_half_open_overlap_and_closed_items_need_includeClosed()
    {
        var office = await OfficeAsync();
        var day = new DateOnly(2027, 04, 05);
        var start = MidnightUtc(day);
        var overlapping = await CreateEventAsync(office, start.AddHours(-1), start.AddHours(1), title: "Sobreposição");
        var cancelled = await CreateEventAsync(office, start.AddHours(2), start.AddHours(3), title: "Cancelado");
        var task = await CreateTaskAsync(office, day, title: "Concluída");
        overlapping.Status.ShouldBe(HttpStatusCode.Created);
        cancelled.Status.ShouldBe(HttpStatusCode.Created);
        task.Status.ShouldBe(HttpStatusCode.Created);
        var cancelledId = cancelled.Body.GetProperty("id").GetGuid();
        var taskId = task.Body.GetProperty("id").GetGuid();
        (await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/schedule/events/{cancelledId}/cancel")).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office.Admin, HttpMethod.Post, $"/api/v1/tasks/{taskId}/state", new { status = "completed" })).Status.ShouldBe(HttpStatusCode.OK);

        var (openStatus, open) = await AgendaAsync(office.Admin, day, day.AddDays(1));
        var (closedStatus, withClosed) = await AgendaAsync(office.Admin, day, day.AddDays(1), includeClosed: true);
        var openIds = open.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        var allIds = withClosed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();

        openStatus.ShouldBe(HttpStatusCode.OK, open.ToString());
        closedStatus.ShouldBe(HttpStatusCode.OK, withClosed.ToString());
        openIds.ShouldContain(overlapping.Body.GetProperty("id").GetGuid());
        openIds.ShouldNotContain(cancelledId);
        openIds.ShouldNotContain(taskId);
        allIds.ShouldContain(cancelledId);
        allIds.ShouldContain(taskId);
        allIds.ShouldContain(overlapping.Body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Agenda_filters_by_assignee_and_team_and_respects_own_scope()
    {
        var office = await OfficeAsync();
        var colleague = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Organization, null, "cases.read", "schedule.events.read");
        var teamId = await _api.CreateAsync("/api/v1/organization/teams", office.Admin.UserId, office.Admin.OrganizationId,
            new { name = $"Equipe {Guid.CreateVersion7():N}" });
        (await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/organization/teams/{teamId}/members", new { memberIds = new[] { colleague.MemberId } }))
            .Status.ShouldBe(HttpStatusCode.OK);
        var day = new DateOnly(2027, 04, 05);
        var instant = MidnightUtc(day);
        var adminEvent = await CreateEventAsync(office, instant, instant.AddHours(1), assignee: office.Admin.UserId, title: "Admin");
        var teamEvent = await CreateEventAsync(office, instant.AddHours(2), instant.AddHours(3), assignee: colleague.UserId, title: "Equipe");
        var ownReader = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Own, null, "cases.read", "schedule.events.read");
        var ownEvent = await CreateEventAsync(office, instant.AddHours(4), instant.AddHours(5), assignee: ownReader.UserId, title: "Próprio");

        var (teamStatus, teamAgenda) = await AgendaAsync(office.Admin, day, day.AddDays(1), teamId: teamId);
        var (assigneeStatus, assigneeAgenda) = await AgendaAsync(office.Admin, day, day.AddDays(1), assignee: colleague.UserId);
        var (ownStatus, ownAgenda) = await AgendaAsync(ownReader, day, day.AddDays(1));

        teamStatus.ShouldBe(HttpStatusCode.OK, teamAgenda.ToString());
        teamAgenda.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldBe([teamEvent.Body.GetProperty("id").GetGuid()]);
        assigneeStatus.ShouldBe(HttpStatusCode.OK, assigneeAgenda.ToString());
        assigneeAgenda.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ShouldBe([teamEvent.Body.GetProperty("id").GetGuid()]);
        ownStatus.ShouldBe(HttpStatusCode.OK, ownAgenda.ToString());
        ownAgenda.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())
            .ShouldBe([ownEvent.Body.GetProperty("id").GetGuid()]);
        adminEvent.Body.GetProperty("id").GetGuid().ShouldNotBe(teamEvent.Body.GetProperty("id").GetGuid());
        adminEvent.Body.GetProperty("id").GetGuid().ShouldNotBe(ownEvent.Body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Agenda_omits_unreadable_sources_and_restricted_cases_and_filters_hearing_by_designated_lawyer()
    {
        var office = await OfficeAsync();
        var day = new DateOnly(2027, 04, 05);
        var instant = MidnightUtc(day);
        var eventReader = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Own, null, "schedule.events.read", "schedule.events.manage", "cases.read");
        var administrativeEvent = await CreateEventAsync(office, instant, instant.AddHours(1), assignee: eventReader.UserId, title: "Evento próprio");
        var task = await CreateTaskAsync(office, day, title: "Sem permissão de leitura");
        var deadline = await CreateDeadlineAsync(office);
        var (sourceStatus, visibleSources) = await AgendaAsync(eventReader, day, day.AddDays(1));

        sourceStatus.ShouldBe(HttpStatusCode.OK, visibleSources.ToString());
        visibleSources.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid())
            .ShouldBe([administrativeEvent.Body.GetProperty("id").GetGuid()]);
        task.Body.GetProperty("id").GetGuid().ShouldNotBe(administrativeEvent.Body.GetProperty("id").GetGuid());
        deadline.Id.ShouldNotBe(Guid.Empty);

        var assignedLawyer = await TestOrganizations.AddScopedMemberAsync(_dependencies, office.Admin.OrganizationId,
            Guid.CreateVersion7(), DataScope.Organization, null, "cases.read", "cases.edit", "schedule.events.read");
        var caseId = await CaseAsync(office.Admin, assignedLawyer.UserId);
        var hearing = await ScheduleHearingAsync(office, instant, caseId, assignedLawyer.UserId);
        var (designatedStatus, designated) = await AgendaAsync(office.Admin, day, day.AddDays(1), assignee: assignedLawyer.UserId);
        var (otherStatus, other) = await AgendaAsync(office.Admin, day, day.AddDays(1), assignee: office.Admin.UserId);
        designatedStatus.ShouldBe(HttpStatusCode.OK, designated.ToString());
        designated.GetProperty("items").EnumerateArray().ToArray().ShouldContain(i => i.GetProperty("id").GetGuid() == hearing.Body.GetProperty("id").GetGuid());
        otherStatus.ShouldBe(HttpStatusCode.OK, other.ToString());
        other.GetProperty("items").EnumerateArray().ToArray().ShouldNotContain(i => i.GetProperty("id").GetGuid() == hearing.Body.GetProperty("id").GetGuid());

        var restrictedCaseId = await CaseAsync(office.Admin, office.Admin.UserId);
        var restriction = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/cases/{restrictedCaseId}/restrictions",
            new { allowed = new[] { new { userId = office.Admin.UserId } }, blocked = new[] { new { userId = eventReader.UserId } } });
        restriction.Status.ShouldBe(HttpStatusCode.OK, restriction.Body.ToString());
        var secretEvent = await CreateEventAsync(office, instant.AddHours(4), instant.AddHours(5), restrictedCaseId,
            office.Admin.UserId, "Caso sigiloso");
        var (_, restrictedAgenda) = await AgendaAsync(eventReader, day, day.AddDays(1));
        restrictedAgenda.GetProperty("items").EnumerateArray().ToArray().ShouldNotContain(i => i.GetProperty("id").GetGuid() == secretEvent.Body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Agenda_rejects_invalid_ranges_dates_and_cursors()
    {
        var office = await OfficeAsync();
        var (rangeStatus, range) = await SendAsync(office.Admin, HttpMethod.Get, "/api/v1/schedule/agenda?from=2027-04-05&until=2027-04-05");
        var (dateStatus, date) = await SendAsync(office.Admin, HttpMethod.Get, "/api/v1/schedule/agenda?from=invalid&until=2027-04-06");
        var (cursorStatus, cursor) = await AgendaAsync(office.Admin, new DateOnly(2027, 04, 05), new DateOnly(2027, 04, 06), cursor: "invalid-cursor");

        rangeStatus.ShouldBe(HttpStatusCode.BadRequest, range.ToString());
        OrganizationApi.Code(range).ShouldBe("scheduling.agenda_invalid");
        dateStatus.ShouldBe(HttpStatusCode.BadRequest);
        OrganizationApi.Code(date).ShouldBe("http.bad_request");
        if (date.TryGetProperty("detail", out var detail)) { detail.ValueKind.ShouldBe(JsonValueKind.Null); }
        cursorStatus.ShouldBe(HttpStatusCode.BadRequest, cursor.ToString());
        OrganizationApi.Code(cursor).ShouldBe("scheduling.agenda_invalid");
    }
}

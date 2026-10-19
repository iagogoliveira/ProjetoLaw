using System.Net;
using System.Text.Json;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class PublicationWorkflowTests
{
    [Fact]
    public async Task Confirmed_deadline_triage_creates_destination_and_audited_decision()
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        var (status, body) = await TriageAsync(office, publication, DeadlineInput(office, caseId));
        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("status").GetString().ShouldBe("triaged");
        var deadlineId = body.GetProperty("triage").GetProperty("deadlineId").GetGuid();

        var deadlines = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/deadlines");
        deadlines.Status.ShouldBe(HttpStatusCode.OK);
        var deadline = deadlines.Body.EnumerateArray().Single();
        deadline.GetProperty("id").GetGuid().ShouldBe(deadlineId);
        deadline.GetProperty("publicationId").GetGuid().ShouldBe(publication);
        deadline.GetProperty("confirmedBy").GetGuid().ShouldBe(office.UserId);
        deadline.GetProperty("fatalDueOn").GetString().ShouldBe(_clock.Today.AddDays(15).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        var detail = await SendAsync(office, HttpMethod.Get, $"/api/v1/publications/{publication}");
        detail.Body.GetProperty("triage").GetProperty("triagedBy").GetGuid().ShouldBe(office.UserId);
        await AssertWorkflowRowsAsync(office, publication, 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failure_after_destination_or_after_both_saves_rolls_back_data_and_audit(bool afterBothSaves)
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        _failure.AfterTarget = !afterBothSaves;
        _failure.AfterBothSaves = afterBothSaves;

        var failed = await TriageAsync(office, publication, DeadlineInput(office, caseId));
        failed.Status.ShouldBe(afterBothSaves ? HttpStatusCode.InternalServerError : HttpStatusCode.UnprocessableEntity);
        await AssertWorkflowRowsAsync(office, publication, 0, 0);
        var detail = await SendAsync(office, HttpMethod.Get, $"/api/v1/publications/{publication}");
        detail.Body.GetProperty("status").GetString().ShouldBe("pending");
        detail.Body.GetProperty("legalCaseId").ValueKind.ShouldBe(JsonValueKind.Null);

        _failure.AfterTarget = false;
        _failure.AfterBothSaves = false;
        (await TriageAsync(office, publication, DeadlineInput(office, caseId))).Status.ShouldBe(HttpStatusCode.OK);
        await AssertWorkflowRowsAsync(office, publication, 1, 1);
    }

    [Fact]
    public async Task Concurrent_triage_produces_one_destination_and_one_decision()
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => TriageAsync(office, publication, DeadlineInput(office, caseId))));
        responses.Count(r => r.Status == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(r => r.Status == HttpStatusCode.Conflict).ShouldBe(5);
        await AssertWorkflowRowsAsync(office, publication, 1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unconfirmed_or_invalid_target_is_not_persisted(bool invalidTarget)
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        var result = await TriageAsync(office, publication, DeadlineInput(office, caseId, invalidTarget, invalidTarget ? Guid.NewGuid() : null));
        result.Status.ShouldBe(HttpStatusCode.BadRequest, result.Body.ToString());
        await AssertWorkflowRowsAsync(office, publication, 0, 0);
    }

    [Fact]
    public async Task Destination_permission_is_required_even_for_authorized_triager()
    {
        var office = await OfficeAsync();
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "publications.read", "publications.triage", "cases.read");
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        var result = await TriageAsync(reader, publication, DeadlineInput(office, caseId));
        result.Status.ShouldBe(HttpStatusCode.NotFound, result.Body.ToString());
        await AssertWorkflowRowsAsync(office, publication, 0, 0);
    }

    [Fact]
    public async Task Irrelevant_publication_is_closed_without_link_with_reason_and_author()
    {
        var office = await OfficeAsync();
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        (await TriageAsync(office, publication, Discard(null))).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await TriageAsync(office, publication, Discard(confirmed: false))).Status.ShouldBe(HttpStatusCode.BadRequest);
        var (status, body) = await TriageAsync(office, publication, Discard("Captura por homônimo"));
        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.GetProperty("caseId").ValueKind.ShouldBe(JsonValueKind.Null);
        var triage = body.GetProperty("triage");
        triage.GetProperty("reason").GetString().ShouldBe("Captura por homônimo");
        triage.GetProperty("triagedBy").GetGuid().ShouldBe(office.UserId);
        (await TriageAsync(office, publication, Discard())).Status.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Task_and_hearing_are_created_with_their_own_permissions_and_case(bool hearing)
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        object input = hearing ? new
        {
            classification = "hearing",
            action = "hearing",
            confirmed = true,
            caseId,
            hearing = new { type = "conciliation", format = "virtual", scheduledAt = "2026-12-01T10:00:00-03:00", meetingLink = "https://meet.example.com/sala", lawyerUserId = office.UserId },
        } : new
        {
            classification = "decision",
            action = "task",
            confirmed = true,
            caseId,
            task = new { title = "Analisar decisão", dueOn = _clock.Today.AddDays(1), assigneeUserId = office.UserId },
        };
        var result = await TriageAsync(office, publication, input);
        result.Status.ShouldBe(HttpStatusCode.OK, result.Body.ToString());
        var destinationId = result.Body.GetProperty("triage").GetProperty(hearing ? "hearingId" : "taskId").GetGuid();
        var destination = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/{(hearing ? "hearings" : "tasks")}");
        destination.Status.ShouldBe(HttpStatusCode.OK);
        destination.Body.EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(destinationId);
    }

    [Fact]
    public async Task Scope_and_hidden_candidates_protect_link_assignment_and_triage()
    {
        var office = await OfficeAsync();
        var limited = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "publications.read", "publications.triage", "publications.distribute", "cases.read", "schedule.deadlines.manage");
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, cnj: cnj);
        var publication = await ReceiveAsync(office, await SourceAsync(office), cnj + " 6616920-53.2022.4.01.1974");
        (await TriageAsync(limited, publication, Discard())).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(limited, HttpMethod.Put, $"/api/v1/publications/{publication}/link", new { caseId })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(limited, HttpMethod.Put, $"/api/v1/publications/{publication}/assignment", new { assigneeUserId = limited.UserId })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(limited, HttpMethod.Get, $"/api/v1/cases/{caseId}/deadlines")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Denied_writes_to_linked_restricted_case_keep_the_existing_access_denial_audit()
    {
        var office = await OfficeAsync();
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            "publications.read", "publications.triage", "publications.distribute", "cases.read");
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, cnj: cnj);
        var publication = await ReceiveAsync(office, await SourceAsync(office), "Texto " + cnj);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions",
            new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = reader.UserId } } })).Status.ShouldBe(HttpStatusCode.OK);
        (await TriageAsync(reader, publication, Discard())).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(reader, HttpMethod.Put, $"/api/v1/publications/{publication}/link", new { caseId })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await SendAsync(reader, HttpMethod.Put, $"/api/v1/publications/{publication}/assignment", new { assigneeUserId = reader.UserId })).Status.ShouldBe(HttpStatusCode.NotFound);
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<PublicationsDbContext>();
            var entityId = caseId.ToString();
            (await db.Set<AuditLogEntry>().CountAsync(a => a.OrganizationId == office.OrganizationId && a.UserId == reader.UserId
                && a.EntityId == entityId && a.Action == AuditActions.AccessDenied, Ct)).ShouldBeGreaterThanOrEqualTo(3);
        });
    }

    private async Task AssertWorkflowRowsAsync(SeededMember office, Guid publication, int triages, int deadlines)
    {
        await InOfficeAsync(office, async services =>
        {
            var publications = services.GetRequiredService<PublicationsDbContext>();
            var scheduling = services.GetRequiredService<SchedulingDbContext>();
            (await publications.Triages.CountAsync(t => t.PublicationId == publication, Ct)).ShouldBe(triages);
            var ids = await scheduling.Deadlines.Where(d => d.PublicationId == publication).Select(d => d.Id.ToString()).ToListAsync(Ct);
            ids.Count.ShouldBe(deadlines);
            var audits = await publications.Set<AuditLogEntry>().Where(a => a.EntityType == "Deadline" && ids.Contains(a.EntityId)).CountAsync(Ct);
            audits.ShouldBe(deadlines);
            if (deadlines == 0)
            {
                // Nenhum prazo foi criado neste escritório antes da falha, portanto nem sua auditoria pode sobreviver.
                (await publications.Set<AuditLogEntry>().CountAsync(a => a.OrganizationId == office.OrganizationId && a.EntityType == "Deadline", Ct)).ShouldBe(0);
            }
        });
    }
}

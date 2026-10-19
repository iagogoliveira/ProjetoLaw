using System.Net;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Infrastructure.Persistence;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class PublicationWorkflowTests
{
    private object WorkInput(SeededMember office, Guid caseId, bool hearing, Guid? assignee = null) => hearing ? new
    {
        classification = "hearing",
        action = "hearing",
        confirmed = true,
        caseId,
        hearing = new { type = "conciliation", format = "virtual", scheduledAt = "2026-12-01T10:00:00Z", meetingLink = "https://meet.example.com/sala", lawyerUserId = assignee ?? office.UserId },
    } : new
    {
        classification = "decision",
        action = "task",
        confirmed = true,
        caseId,
        task = new { title = "Analisar decisão", dueOn = _clock.Today.AddDays(1), assigneeUserId = assignee ?? office.UserId },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Task_and_hearing_also_roll_back_after_both_saves(bool hearing)
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        _failure.AfterBothSaves = true;
        (await TriageAsync(office, publication, WorkInput(office, caseId, hearing))).Status.ShouldBe(HttpStatusCode.InternalServerError);
        var destinations = await SendAsync(office, HttpMethod.Get, $"/api/v1/cases/{caseId}/{(hearing ? "hearings" : "tasks")}");
        destinations.Status.ShouldBe(HttpStatusCode.OK);
        destinations.Body.GetArrayLength().ShouldBe(0);
        await AssertWorkflowRowsAsync(office, publication, 0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Task_and_hearing_require_destination_permission_and_responsible_scope(bool hearing)
    {
        var office = await OfficeAsync();
        var triager = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "publications.read", "publications.triage", "cases.read");
        var limited = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null,
            "cases.read", "schedule.tasks.read");
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        (await TriageAsync(triager, publication, WorkInput(office, caseId, hearing))).Status.ShouldBe(HttpStatusCode.NotFound);
        (await TriageAsync(office, publication, WorkInput(office, caseId, hearing, limited.UserId))).Status.ShouldBe(HttpStatusCode.BadRequest);
        await AssertWorkflowRowsAsync(office, publication, 0, 0);
    }

    [Fact]
    public async Task Closed_case_does_not_receive_a_new_deadline()
    {
        var office = await OfficeAsync();
        var caseId = await CaseAsync(office);
        var publication = await ReceiveAsync(office, await SourceAsync(office));
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<LitigationDbContext>();
            var legalCase = await db.Cases.SingleAsync(c => c.Id == caseId, Ct);
            legalCase.Close(new ClosingInput(Guid.CreateVersion7(), (CaseOutcome)0, null, _clock.Today, null)).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(Ct);
        });
        var result = await TriageAsync(office, publication, DeadlineInput(office, caseId));
        result.Status.ShouldBe(HttpStatusCode.UnprocessableEntity, result.Body.ToString());
        OrganizationApi.Code(result.Body).ShouldBe("scheduling.case_unavailable");
        await AssertWorkflowRowsAsync(office, publication, 0, 0);
    }

    [Fact]
    public async Task Known_unresolved_capture_alert_from_previous_day_blocks_closing()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        await CompleteCaptureAsync(office, source);
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<PublicationsDbContext>();
            db.Alerts.Add(CaptureAlert.Raise(source, _clock.Today.AddDays(-1), _clock.UtcNow.AddDays(-1), "publications.capture_missing"));
            await db.SaveChangesAsync(Ct);
        });
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.Conflict);
        (await CloseStateAsync(office)).Body.GetProperty("missingCaptureCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Legacy_success_matches_its_active_period_and_cannot_cover_a_new_activation()
    {
        var office = await OfficeAsync();
        var sourceId = Guid.Empty;
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<PublicationsDbContext>();
            var source = CaptureSource.Create(new(CaptureSourceKind.Name, "Legada", "Nome")).Value;
            db.Sources.Add(source);
            await db.SaveChangesAsync(Ct);
            sourceId = source.Id;
            db.SourcePeriods.Add(CaptureSourcePeriod.Start(source.Id, _clock.UtcNow.AddSeconds(1)));
            var run = CaptureRun.Start(source.Id, _clock.Today, _clock.UtcNow.AddSeconds(2), source.CreatedAt).Value;
            run.Finish(0, null, run.StartedAt);
            db.Runs.Add(run);
            await db.SaveChangesAsync(Ct);
        });
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{sourceId}",
            new { kind = "name", label = "Legada", value = "Nome", isActive = false })).Status.ShouldBe(HttpStatusCode.OK);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{sourceId}",
            new { kind = "name", label = "Legada", value = "Nome", isActive = true })).Status.ShouldBe(HttpStatusCode.OK);
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.Conflict);
        await CompleteCaptureAsync(office, sourceId);
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.OK);
    }
}

using System.Net;
using System.Text.Json;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class PublicationWorkflowTests
{
    private Task<(HttpStatusCode Status, JsonElement Body)> CloseAsync(SeededMember office) =>
        SendAsync(office, HttpMethod.Post, "/api/v1/publications/daily-close", new { day = _clock.Today });
    private Task<(HttpStatusCode Status, JsonElement Body)> CloseStateAsync(SeededMember office, DateOnly? day = null, string? extra = null) =>
        SendAsync(office, HttpMethod.Get, $"/api/v1/publications/daily-close?day={day ?? _clock.Today:yyyy-MM-dd}{extra}");

    [Fact]
    public async Task Missing_or_failed_capture_and_pending_triage_prevent_closing_but_empty_success_counts()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        var absent = await CloseAsync(office);
        absent.Status.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(absent.Body).ShouldBe("publications.close_incomplete_capture");
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<PublicationsDbContext>();
            var run = CaptureRun.Start(source, _clock.Today, _clock.UtcNow).Value;
            run.Finish(0, null, _clock.UtcNow);
            db.Runs.Add(run);
            await db.SaveChangesAsync(Ct);
        });
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.Conflict);
        await CompleteCaptureAsync(office, source, "publications.provider_unavailable");
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.Conflict);
        await CompleteCaptureAsync(office, source);
        var publication = await ReceiveAsync(office, source);
        var pending = await CloseAsync(office);
        pending.Status.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(pending.Body).ShouldBe("publications.close_pending_triage");
        (await TriageAsync(office, publication, Discard())).Status.ShouldBe(HttpStatusCode.OK);
        var closed = await CloseAsync(office);
        closed.Status.ShouldBe(HttpStatusCode.OK, closed.Body.ToString());
        closed.Body.GetProperty("closedBy").GetGuid().ShouldBe(office.UserId);
        (await CloseAsync(office)).Body.GetProperty("id").GetGuid().ShouldBe(closed.Body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task New_content_reopens_day_and_second_closing_preserves_signed_history()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        await CompleteCaptureAsync(office, source);
        var first = await CloseAsync(office);
        first.Status.ShouldBe(HttpStatusCode.OK, first.Body.ToString());
        var firstId = first.Body.GetProperty("id").GetGuid();
        var publication = await ReceiveAsync(office, source);
        var open = await CloseStateAsync(office);
        open.Status.ShouldBe(HttpStatusCode.OK);
        open.Body.GetProperty("isClosed").GetBoolean().ShouldBeFalse();
        open.Body.GetProperty("pendingCount").GetInt32().ShouldBe(1);
        open.Body.GetProperty("latest").GetProperty("id").GetGuid().ShouldBe(firstId);
        (await TriageAsync(office, publication, Discard())).Status.ShouldBe(HttpStatusCode.OK);
        (await CloseStateAsync(office)).Body.GetProperty("isClosed").GetBoolean().ShouldBeFalse();
        var second = await CloseAsync(office);
        second.Status.ShouldBe(HttpStatusCode.OK);
        second.Body.GetProperty("revision").GetInt32().ShouldBe(2);
        var state = await CloseStateAsync(office, extra: "&limit=1");
        state.Body.GetProperty("isClosed").GetBoolean().ShouldBeTrue();
        state.Body.GetProperty("history").GetArrayLength().ShouldBe(1);
        state.Body.GetProperty("nextRevision").GetInt32().ShouldBe(2);
        var previous = await CloseStateAsync(office, extra: "&limit=1&beforeRevision=2");
        var old = previous.Body.GetProperty("history").EnumerateArray().Single();
        old.GetProperty("id").GetGuid().ShouldBe(firstId);
        old.GetProperty("publicationCount").GetInt32().ShouldBe(0);
        old.GetProperty("closedAt").GetDateTimeOffset().ShouldBe(first.Body.GetProperty("closedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Concurrent_repeated_closes_create_only_one_revision()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        await CompleteCaptureAsync(office, source);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CloseAsync(office)));
        results.ShouldAllBe(r => r.Status == HttpStatusCode.OK);
        results.Select(r => r.Body.GetProperty("id").GetGuid()).Distinct().Count().ShouldBe(1);
        (await CloseStateAsync(office)).Body.GetProperty("history").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Receipt_day_in_sao_paulo_includes_previous_pending_and_excludes_next_local_day()
    {
        var office = await OfficeAsync();
        _clock.UtcNow = new(2026, 10, 19, 2, 30, 0, TimeSpan.Zero); // 18/10, 23h30 local
        var source = await SourceAsync(office);
        var earlier = await ReceiveAsync(office, source);
        _clock.UtcNow = new(2026, 10, 20, 2, 30, 0, TimeSpan.Zero); // 19/10, 23h30 local
        await CompleteCaptureAsync(office, source);
        (await CloseStateAsync(office)).Body.GetProperty("pendingCount").GetInt32().ShouldBe(1);
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.Conflict);
        (await TriageAsync(office, earlier, Discard())).Status.ShouldBe(HttpStatusCode.OK);
        var late = await ReceiveAsync(office, source);
        (await TriageAsync(office, late, Discard())).Status.ShouldBe(HttpStatusCode.OK);
        var closedDay = _clock.Today;
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.OK);
        _clock.UtcNow = new(2026, 10, 20, 3, 0, 0, TimeSpan.Zero); // 20/10, 00h00 local
        await ReceiveAsync(office, source);
        (await CloseStateAsync(office, closedDay)).Body.GetProperty("isClosed").GetBoolean().ShouldBeTrue();
        (await CloseStateAsync(office)).Body.GetProperty("pendingCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Source_reactivation_requires_its_new_capture_without_losing_old_closings()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        await CompleteCaptureAsync(office, source);
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.OK);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{source}",
            new { kind = "name", label = "Escritório", value = "Nome monitorado", isActive = false })).Status.ShouldBe(HttpStatusCode.OK);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{source}",
            new { kind = "name", label = "Escritório", value = "Nome monitorado", isActive = true })).Status.ShouldBe(HttpStatusCode.OK);
        (await CloseStateAsync(office)).Body.GetProperty("missingCaptureCount").GetInt32().ShouldBe(1);
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.Conflict);
        await CompleteCaptureAsync(office, source);
        var closed = await CloseAsync(office);
        closed.Status.ShouldBe(HttpStatusCode.OK, closed.Body.ToString());
        closed.Body.GetProperty("revision").GetInt32().ShouldBe(2);
        (await CloseStateAsync(office)).Body.GetProperty("history").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Disabling_source_cannot_erase_capture_expected_earlier_that_day()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{source}",
            new { kind = "name", label = "Escritório", value = "Nome monitorado", isActive = false })).Status.ShouldBe(HttpStatusCode.OK);
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.Conflict);
        await CompleteCaptureAsync(office, source);
        (await CloseAsync(office)).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Closing_requires_office_scope_and_read_access_to_every_publication()
    {
        var office = await OfficeAsync();
        var limited = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(),
            LexFlow.Organization.Domain.Roles.DataScope.Own, null, "publications.close", "publications.read");
        (await CloseAsync(limited)).Status.ShouldBe(HttpStatusCode.Forbidden);
        var reader = await TestOrganizations.AddMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), "publications.close", "publications.read");
        const string cnj = "2238058-98.2025.8.26.8434";
        var caseId = await CaseAsync(office, cnj: cnj);
        await ReceiveAsync(office, await SourceAsync(office), "Texto " + cnj);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/cases/{caseId}/restrictions",
            new { allowed = Array.Empty<object>(), blocked = new[] { new { userId = reader.UserId } } })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(reader, HttpMethod.Get, "/api/v1/publications")).Body.GetProperty("items").GetArrayLength().ShouldBe(0);
        (await CloseStateAsync(reader)).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await CloseAsync(reader)).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Future_and_default_closing_days_and_bad_history_limits_are_rejected()
    {
        var office = await OfficeAsync();
        (await SendAsync(office, HttpMethod.Post, "/api/v1/publications/daily-close", new { day = _clock.Today.AddDays(1) })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Post, "/api/v1/publications/daily-close", new { day = default(DateOnly) })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await CloseStateAsync(office, extra: "&limit=0")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await CloseStateAsync(office, extra: "&beforeRevision=0")).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Concurrent_intake_leaves_new_content_pending_even_if_closing_wins_the_lock()
    {
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        await CompleteCaptureAsync(office, source);
        var closeTask = CloseAsync(office);
        var intakeTask = ReceiveAsync(office, source);
        await Task.WhenAll(closeTask, intakeTask);
        (await closeTask).Status.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);
        var state = await CloseStateAsync(office);
        state.Body.GetProperty("isClosed").GetBoolean().ShouldBeFalse();
        state.Body.GetProperty("pendingCount").GetInt32().ShouldBe(1);
    }
}

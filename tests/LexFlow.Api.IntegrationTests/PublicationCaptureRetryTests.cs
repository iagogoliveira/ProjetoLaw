using System.Net;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Capture;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class PublicationWorkflowTests
{
    [Fact]
    public async Task Manual_capture_is_audited_delivered_after_commit_and_deduplicated_outside_automatic_window()
    {
        _clock.UtcNow = new(2026, 10, 19, 5, 0, 0, TimeSpan.Zero); // 02h local
        var office = await OfficeAsync();
        var source = await SourceAsync(office);
        var result = await SendAsync(office, HttpMethod.Post, $"/api/v1/publication-sources/{source}/capture", new { day = _clock.Today });
        result.Status.ShouldBe(HttpStatusCode.Accepted, result.Body.ToString());
        var requestId = result.Body.GetProperty("requestId").GetGuid();
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<PublicationsDbContext>();
            var audit = await db.Set<AuditLogEntry>().SingleAsync(a => a.Id == requestId, Ct);
            audit.Action.ShouldBe("CaptureRetryRequested"); audit.UserId.ShouldBe(office.UserId);
            audit.EntityId.ShouldBe(source.ToString());
        });
        await DeliverRetryAsync(office);
        await _factory.RunJobsAsync(office.OrganizationId);
        var count = await SendAsync(office, HttpMethod.Get, "/api/v1/publications/pending-count");
        count.Body.GetProperty("count").GetInt32().ShouldBe(2);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/publication-sources/{source}/capture", new { day = _clock.Today })).Status.ShouldBe(HttpStatusCode.Accepted);
        await DeliverRetryAsync(office); await _factory.RunJobsAsync(office.OrganizationId);
        (await SendAsync(office, HttpMethod.Get, "/api/v1/publications/pending-count")).Body.GetProperty("count").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Manual_capture_requires_office_management_active_source_and_an_eligible_day()
    {
        var office = await OfficeAsync(); var source = await SourceAsync(office);
        var limited = await TestOrganizations.AddScopedMemberAsync(dependencies, office.OrganizationId, Guid.CreateVersion7(), DataScope.Own, null, "settings.integrations.manage");
        (await SendAsync(limited, HttpMethod.Post, $"/api/v1/publication-sources/{source}/capture", new { day = _clock.Today })).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/publication-sources/{source}/capture", new { day = _clock.Today.AddDays(1) })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Get, $"/api/v1/publication-sources/{source}/capture?day={_clock.Today.AddDays(1):yyyy-MM-dd}")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/publication-sources/{source}/capture", new { day = _clock.Today.AddDays(-1) })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{source}", new { kind = "name", label = "Fonte", value = "Nome", isActive = false })).Status.ShouldBe(HttpStatusCode.OK);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/publication-sources/{source}/capture", new { day = _clock.Today })).Status.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Historical_capture_recovers_an_earlier_day_and_resolves_its_alert()
    {
        var office = await OfficeAsync(); var source = await SourceAsync(office); var day = _clock.Today;
        await InOfficeAsync(office, async services =>
        {
            var db = services.GetRequiredService<PublicationsDbContext>();
            db.Alerts.Add(CaptureAlert.Raise(source, day, _clock.UtcNow, "publications.capture_missing"));
            await db.SaveChangesAsync(Ct);
        });
        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/publication-sources/{source}/capture", new { day })).Status.ShouldBe(HttpStatusCode.Accepted);
        await DeliverRetryAsync(office); await _factory.RunJobsAsync(office.OrganizationId);
        var state = await SendAsync(office, HttpMethod.Get, $"/api/v1/publication-sources/{source}/capture?day={day:yyyy-MM-dd}");
        state.Status.ShouldBe(HttpStatusCode.OK);
        state.Body.GetProperty("runs").GetArrayLength().ShouldBe(1);
        state.Body.GetProperty("alert").GetProperty("resolvedAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.String);
    }

    private async Task DeliverRetryAsync(SeededMember office)
    {
        var original = _factory.PublishedEvents.OfType<CaptureRetryRequestedIntegrationEvent>().Last();
        var delivered = original with { EventId = Guid.CreateVersion7(), OrganizationId = office.OrganizationId, OccurredAt = _clock.UtcNow };
        await InOfficeAsync(office, async services =>
        {
            var consumer = ActivatorUtilities.CreateInstance<CaptureRetryConsumer>(services);
            await consumer.Handle(delivered, Ct);
            await consumer.Handle(delivered, Ct);
        });
    }
}

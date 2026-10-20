using System.Net;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class PublicationWorkflowTests
{
    [Fact]
    public async Task Source_activation_and_period_share_one_instant_with_an_advancing_clock()
    {
        _clock.AdvanceOnRead = TimeSpan.FromMilliseconds(1);
        var office = await OfficeAsync();
        var sourceId = await SourceAsync(office);
        await AssertCurrentPeriodAsync(office, sourceId);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{sourceId}",
            new { kind = "name", label = "Fonte", value = "Outro nome" })).Status.ShouldBe(HttpStatusCode.OK);
        await AssertCurrentPeriodAsync(office, sourceId);
    }

    [Fact]
    public async Task Capture_matches_the_period_identity_even_when_two_activations_share_the_same_timestamp()
    {
        var office = await OfficeAsync(); var source = await SourceAsync(office);
        await CaptureNowAsync(office, source);
        (await CloseStateAsync(office)).Body.GetProperty("missingCaptureCount").GetInt32().ShouldBe(0);
        (await SendAsync(office, HttpMethod.Put, $"/api/v1/publication-sources/{source}",
            new { kind = "name", label = "Fonte", value = "Outro nome" })).Status.ShouldBe(HttpStatusCode.OK);
        (await CloseStateAsync(office)).Body.GetProperty("missingCaptureCount").GetInt32().ShouldBe(1);
        _clock.UtcNow = _clock.UtcNow.AddHours(2);
        await InOfficeAsync(office, async services =>
        {
            await services.GetRequiredService<CaptureMonitor>().RunAsync(source, _clock.Today);
            (await services.GetRequiredService<PublicationsDbContext>().Alerts.CountAsync(a => a.SourceId == source && a.ResolvedAt == null, Ct)).ShouldBe(1);
        });
        await CaptureNowAsync(office, source);
        (await CloseStateAsync(office)).Body.GetProperty("missingCaptureCount").GetInt32().ShouldBe(0);
        await InOfficeAsync(office, async services =>
        {
            var runs = await services.GetRequiredService<PublicationsDbContext>().Runs.Where(r => r.SourceId == source).ToListAsync(Ct);
            runs.Count.ShouldBe(2);
            runs.Select(r => r.SourcePeriodId).Distinct().Count().ShouldBe(2);
            runs.ShouldAllBe(r => r.SourcePeriodId != null);
        });
    }

    private async Task CaptureNowAsync(SeededMember office, Guid source)
    {
        (await SendAsync(office, HttpMethod.Post, $"/api/v1/publication-sources/{source}/capture", new { day = _clock.Today }))
            .Status.ShouldBe(HttpStatusCode.Accepted);
        await DeliverRetryAsync(office);
        await _factory.RunJobsAsync(office.OrganizationId);
    }

    private Task AssertCurrentPeriodAsync(SeededMember office, Guid sourceId) => InOfficeAsync(office, async services =>
    {
        var db = services.GetRequiredService<PublicationsDbContext>();
        var source = await db.Sources.SingleAsync(s => s.Id == sourceId, Ct);
        var period = await db.SourcePeriods.SingleAsync(p => p.SourceId == sourceId && p.DeactivatedAt == null, Ct);
        period.ActivatedAt.ShouldBe(source.ActivatedAt!.Value);
    });
}

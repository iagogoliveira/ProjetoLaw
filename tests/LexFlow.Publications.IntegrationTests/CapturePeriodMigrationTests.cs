using LexFlow.Infrastructure.Common.Context;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Publications.IntegrationTests;

public sealed class CapturePeriodMigrationTests(PublicationsDatabaseFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upgrade_links_only_unambiguous_legacy_runs_without_rewriting_the_activation_history()
    {
        var connection = await fixture.CreateEmptyDatabaseAsync();
        await PublicationsDatabaseFixture.MigrateAsync(connection);
        await using var provider = PublicationsDatabaseFixture.BuildProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PublicationsDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261019195841_TriageAndDailyClose", cancellationToken: Ct);
        var office = Guid.CreateVersion7();
        using var ambient = AmbientContext.Begin(office, "legacy-capture-period");
        var activatedAt = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
        var source = CaptureSource.Create(new(CaptureSourceKind.Name, "Fonte", "Nome")).Value;
        source.ActivateAt(activatedAt);
        var period = CaptureSourcePeriod.Start(source.Id, activatedAt.AddMilliseconds(1));
        var unmatched = CaptureSourcePeriod.Start(source.Id, activatedAt.AddHours(-1));
        unmatched.End(activatedAt.AddMinutes(-30));
        db.Sources.Add(source); db.SourcePeriods.AddRange(period, unmatched);
        await db.SaveChangesAsync(Ct);
        var runId = Guid.CreateVersion7(); var unmatchedId = Guid.CreateVersion7();
        await InsertLegacyRunAsync(db, office, source.Id, runId, activatedAt);
        await InsertLegacyRunAsync(db, office, source.Id, unmatchedId, activatedAt.AddMinutes(-45));
        await migrator.MigrateAsync(cancellationToken: Ct);
        (await db.Runs.SingleAsync(r => r.Id == runId, Ct)).SourcePeriodId.ShouldBe(period.Id);
        (await db.Runs.SingleAsync(r => r.Id == unmatchedId, Ct)).SourcePeriodId.ShouldBeNull();
        (await db.Runs.SingleAsync(r => r.Id == runId, Ct)).SourceActivatedAt.ShouldBe(activatedAt);
        db.ChangeTracker.Clear();
        (await db.SourcePeriods.SingleAsync(p => p.Id == period.Id, Ct)).ActivatedAt.ShouldBe(activatedAt.AddMilliseconds(1));
        (await db.SourcePeriods.SingleAsync(p => p.Id == unmatched.Id, Ct)).DeactivatedAt.ShouldBe(activatedAt.AddMinutes(-30));
        await migrator.MigrateAsync("20261019195841_TriageAndDailyClose", cancellationToken: Ct);
        await migrator.MigrateAsync(cancellationToken: Ct);
        (await db.Runs.AsNoTracking().SingleAsync(r => r.Id == runId, Ct)).SourcePeriodId.ShouldBe(period.Id);
    }

    private static Task<int> InsertLegacyRunAsync(PublicationsDbContext db, Guid office, Guid source, Guid id, DateTimeOffset activatedAt) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO publications.capture_runs
                (id, organization_id, source_id, day, source_activated_at, status, started_at, finished_at, count)
            VALUES ({id}, {office}, {source}, {new DateOnly(2026, 10, 19)}, {activatedAt}, 'Succeeded', {activatedAt}, {activatedAt}, 0)
            """, Ct);
}

using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Publications.Domain;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Publications.IntegrationTests;

public sealed class PublicationsDatabaseTests(PublicationsDatabaseFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_apply_revert_and_reapply_without_pending_model_changes()
    {
        var connection = await fixture.CreateEmptyDatabaseAsync();
        await PublicationsDatabaseFixture.MigrateAsync(connection);
        await using var provider = PublicationsDatabaseFixture.BuildProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PublicationsDbContext>();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        await db.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase, cancellationToken: Ct);
        (await db.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);
    }

    [Fact]
    public async Task Upgrade_preserves_legacy_activation_and_deactivation_history_from_audit()
    {
        var connection = await fixture.CreateEmptyDatabaseAsync();
        await using var provider = PublicationsDatabaseFixture.BuildProvider(connection);
        await using (var setup = provider.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync(Ct);
            await setup.ServiceProvider.GetRequiredService<PublicationsDbContext>().GetService<IMigrator>()
                .MigrateAsync("20261019175139_MovementSynchronization", cancellationToken: Ct);
        }
        var office = Guid.CreateVersion7();
        Guid sourceId;
        using (AmbientContext.Begin(office, "legacy-source"))
        await using (var seed = provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<PublicationsDbContext>();
            var clock = seed.ServiceProvider.GetRequiredService<IClock>();
            var source = CaptureSource.Create(new(CaptureSourceKind.Name, "Fonte", "Escritório")).Value;
            source.ActivateAt(clock.UtcNow);
            sourceId = source.Id;
            db.Sources.Add(source);
            await db.SaveChangesAsync(Ct);
            source.Update(new(CaptureSourceKind.Name, "Fonte", "Escritório", IsActive: false));
            await db.SaveChangesAsync(Ct);
            source.Update(new(CaptureSourceKind.Name, "Fonte", "Escritório"));
            source.ActivateAt(clock.UtcNow);
            await db.SaveChangesAsync(Ct);
        }
        await using (var upgrade = provider.CreateAsyncScope())
        {
            await upgrade.ServiceProvider.GetRequiredService<PublicationsDbContext>().Database.MigrateAsync(Ct);
        }
        using (AmbientContext.Begin(office, "check-source-history"))
        await using (var check = provider.CreateAsyncScope())
        {
            var db = check.ServiceProvider.GetRequiredService<PublicationsDbContext>();
            var periods = await db.SourcePeriods.Where(p => p.SourceId == sourceId).OrderBy(p => p.ActivatedAt).ToListAsync(Ct);
            periods.Count.ShouldBe(2);
            periods[0].DeactivatedAt.ShouldNotBeNull();
            periods[1].DeactivatedAt.ShouldBeNull();
            periods.ShouldAllBe(p => p.OrganizationId == office);
        }
    }

    [Fact]
    public async Task Sources_and_capture_runs_are_persisted_and_filtered_by_tenant()
    {
        var office = Guid.CreateVersion7();
        using (AmbientContext.Begin(office, "publications-tests"))
        await using (var provider = PublicationsDatabaseFixture.BuildProvider(fixture.ConnectionString))
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublicationsDbContext>();
            var source = CaptureSource.Create(new(CaptureSourceKind.Oab, "Advogada", "SP 123456")).Value;
            db.Sources.Add(source);
            var run = CaptureRun.Start(source.Id, new DateOnly(2026, 10, 19), new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero)).Value;
            run.Finish(0, null, run.StartedAt.AddMinutes(1));
            db.Runs.Add(run);
            await db.SaveChangesAsync(Ct);
            db.ChangeTracker.Clear();
            (await db.Sources.SingleAsync(Ct)).Value.ShouldBe("123456/SP");
            (await db.Runs.SingleAsync(Ct)).Status.ShouldBe(CaptureRunStatus.Succeeded);
        }

        using (AmbientContext.Begin(Guid.CreateVersion7(), "other-office"))
        await using (var provider = PublicationsDatabaseFixture.BuildProvider(fixture.ConnectionString))
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublicationsDbContext>();
            (await db.Sources.CountAsync(Ct)).ShouldBe(0);
            (await db.Runs.CountAsync(Ct)).ShouldBe(0);
        }
    }
}

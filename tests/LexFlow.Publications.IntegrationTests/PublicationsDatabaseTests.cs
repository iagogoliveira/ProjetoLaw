using LexFlow.Infrastructure.Common.Context;
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

using LexFlow.Infrastructure.Common.Context;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Scheduling.IntegrationTests;

public sealed class SchedulingDatabaseTests(SchedulingDatabaseFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_apply_revert_and_reapply_without_pending_model_changes()
    {
        var connection = await fixture.CreateEmptyDatabaseAsync();
        await SchedulingDatabaseFixture.MigrateAsync(connection);
        await using var provider = SchedulingDatabaseFixture.BuildProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        await db.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase, cancellationToken: Ct);
        (await db.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);
    }

    [Fact]
    public async Task Deadlines_and_tasks_are_persisted_and_filtered_by_tenant()
    {
        var office = Guid.CreateVersion7();
        using (AmbientContext.Begin(office, "scheduling-tests"))
        await using (var provider = SchedulingDatabaseFixture.BuildProvider(fixture.ConnectionString))
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var user = Guid.CreateVersion7();
            var caseId = Guid.CreateVersion7();
            var day = new DateOnly(2026, 10, 19);
            var now = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
            db.Deadlines.Add(Deadline.Create(caseId, Guid.CreateVersion7(), "Prazo", day, day, day, user, true, user, now).Value);
            db.Tasks.Add(TaskItem.Create(caseId, Guid.CreateVersion7(), "Tarefa", day, user, true, user, now).Value);
            await db.SaveChangesAsync(Ct);
            db.ChangeTracker.Clear();
            (await db.Deadlines.SingleAsync(Ct)).OrganizationId.ShouldBe(office);
            (await db.Tasks.SingleAsync(Ct)).OrganizationId.ShouldBe(office);
        }

        using (AmbientContext.Begin(Guid.CreateVersion7(), "other-office"))
        await using (var provider = SchedulingDatabaseFixture.BuildProvider(fixture.ConnectionString))
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            (await db.Deadlines.CountAsync(Ct)).ShouldBe(0);
            (await db.Tasks.CountAsync(Ct)).ShouldBe(0);
        }
    }
}

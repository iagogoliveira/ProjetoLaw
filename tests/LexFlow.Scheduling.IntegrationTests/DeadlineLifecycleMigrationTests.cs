using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Scheduling.IntegrationTests;

public sealed class DeadlineLifecycleMigrationTests(SchedulingDatabaseFixture fixture)
{
    [Fact]
    public async Task Upgrade_from_7a_preserves_legacy_deadline_dates_origin_and_confirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = await fixture.CreateEmptyDatabaseAsync();
        await using var provider = SchedulingDatabaseFixture.BuildProvider(connection);
        var office = Guid.CreateVersion7();
        using var ambient = AmbientContext.Begin(office, "deadline-upgrade");
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync(ct);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261020185812_OfficeCalendar", cancellationToken: ct);
        var id = Guid.CreateVersion7();
        var caseId = Guid.CreateVersion7();
        var publication = Guid.CreateVersion7();
        var user = Guid.CreateVersion7();
        var start = new DateOnly(2026, 10, 19);
        var internalDate = start.AddDays(5);
        var fatal = start.AddDays(10);
        var now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO scheduling.deadlines
                (id, organization_id, legal_case_id, publication_id, title, start_on, internal_due_on, fatal_due_on,
                 assignee_user_id, confirmed_by, confirmed_at, status, created_at, created_by)
            VALUES ({id}, {office}, {caseId}, {publication}, {"Prazo legado"}, {start}, {internalDate}, {fatal},
                    {user}, {user}, {now}, 'Pending', {now}, {user})
            """, ct);

        await db.GetService<IMigrator>().MigrateAsync(cancellationToken: ct);
        var deadline = await db.Deadlines.SingleAsync(d => d.Id == id, ct);

        deadline.LegalCaseId.ShouldBe(caseId);
        deadline.PublicationId.ShouldBe(publication);
        deadline.StartOn.ShouldBe(start);
        deadline.InternalDueOn.ShouldBe(internalDate);
        deadline.FatalDueOn.ShouldBe(fatal);
        deadline.ConfirmedBy.ShouldBe(user);
        deadline.ConfirmedAt.ShouldBe(now);
        deadline.Type.ShouldBe("procedural");
        deadline.Status.ShouldBe(WorkItemStatus.Pending);
        deadline.RequireReview.ShouldBeFalse();
        deadline.Rule.ShouldBeNull();
        deadline.CalculationSnapshot.ShouldBeNull();
        (await db.DeadlineActivities.CountAsync(ct)).ShouldBe(0);
        db.Database.HasPendingModelChanges().ShouldBeFalse();
    }
}

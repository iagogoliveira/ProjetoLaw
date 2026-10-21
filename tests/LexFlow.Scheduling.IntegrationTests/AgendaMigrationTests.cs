using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Scheduling.IntegrationTests;

public sealed class AgendaMigrationTests(SchedulingDatabaseFixture fixture)
{
    [Fact]
    public async Task Upgrade_from_7b_preserves_legacy_tasks_and_persists_manual_agenda_data_with_tenant_isolation()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = await fixture.CreateEmptyDatabaseAsync();
        await using var provider = SchedulingDatabaseFixture.BuildProvider(connection);
        var office = Guid.CreateVersion7();
        using var ambient = AmbientContext.Begin(office, "agenda-upgrade");
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync(ct);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261020212620_DeadlineLifecycle", cancellationToken: ct);

        var legacy = await SeedLegacyTaskAsync(db, office, ct);
        await migrator.MigrateAsync(cancellationToken: ct);

        await AssertLegacyTaskAsync(db, legacy, office, ct);
        await PersistAndAssertManualAgendaDataAsync(db, office, ct);
        await AssertRlsIsolationAsync(db, office, ct);
        db.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    private static async Task<LegacyTask> SeedLegacyTaskAsync(SchedulingDbContext db, Guid office, CancellationToken ct)
    {
        var values = new LegacyTask(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            new DateOnly(2026, 10, 20), new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero), "Tarefa 7B");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO scheduling.tasks
                (id, organization_id, legal_case_id, publication_id, title, due_on, assignee_user_id,
                 confirmed_by, confirmed_at, status, created_at, created_by)
            VALUES ({values.Id}, {office}, {values.CaseId}, {values.PublicationId}, {values.Title}, {values.DueOn},
                    {values.AssigneeId}, {values.ConfirmedBy}, {values.ConfirmedAt}, {"Pending"},
                    {values.ConfirmedAt}, {values.ConfirmedBy})
            """, ct);
        return values;
    }

    private static async Task AssertLegacyTaskAsync(SchedulingDbContext db, LegacyTask values, Guid office, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.Checklist).Include(t => t.Comments).SingleAsync(t => t.Id == values.Id, ct);

        task.OrganizationId.ShouldBe(office);
        task.LegalCaseId.ShouldBe(values.CaseId);
        task.PublicationId.ShouldBe(values.PublicationId);
        task.AssigneeUserId.ShouldBe(values.AssigneeId);
        task.ConfirmedBy.ShouldBe(values.ConfirmedBy);
        task.ConfirmedAt.ShouldBe(values.ConfirmedAt);
        task.Title.ShouldBe(values.Title);
        task.DueOn.ShouldBe(values.DueOn);
        task.Status.ShouldBe(TaskState.Pending);
        task.Priority.ShouldBe(TaskPriority.Normal);
        task.ParentTaskId.ShouldBeNull();
        task.Description.ShouldBeNull();
        task.ClosedBy.ShouldBeNull();
        task.ClosedAt.ShouldBeNull();
        task.Checklist.ShouldBeEmpty();
        task.Comments.ShouldBeEmpty();
    }

    private static async Task PersistAndAssertManualAgendaDataAsync(SchedulingDbContext db, Guid office, CancellationToken ct)
    {
        var user = Guid.CreateVersion7();
        var now = new DateTimeOffset(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);
        var task = TaskItem.Register(new TaskInput(null, "Tarefa sem processo", "Descrição", new DateOnly(2026, 10, 30),
            user, TaskPriority.High), user, now).Value;
        task.AddChecklist("Conferir documento").IsSuccess.ShouldBeTrue();
        task.AddComment("Nota interna", user, now).IsSuccess.ShouldBeTrue();
        var calendarEvent = CalendarEvent.Create(new CalendarEventInput(null, "Reunião interna", null, now.AddDays(1),
            now.AddDays(1).AddHours(1), user, "Sala 1")).Value;
        db.Tasks.Add(task);
        db.Events.Add(calendarEvent);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        var persistedTask = await db.Tasks.Include(t => t.Checklist).Include(t => t.Comments).SingleAsync(t => t.Id == task.Id, ct);
        var persistedEvent = await db.Events.SingleAsync(e => e.Id == calendarEvent.Id, ct);
        persistedTask.LegalCaseId.ShouldBeNull();
        persistedTask.OrganizationId.ShouldBe(office);
        persistedTask.CreatedAt.ShouldNotBe(default);
        persistedTask.CreatedBy.ShouldBe(Guid.Empty);
        persistedTask.Checklist.Count.ShouldBe(1);
        persistedTask.Comments.Count.ShouldBe(1);
        persistedTask.Checklist.Single().LegalCaseId.ShouldBeNull();
        persistedTask.Checklist.Single().OrganizationId.ShouldBe(office);
        persistedTask.Checklist.Single().CreatedAt.ShouldNotBe(default);
        persistedTask.Checklist.Single().CreatedBy.ShouldBe(Guid.Empty);
        persistedTask.Comments.Single().LegalCaseId.ShouldBeNull();
        persistedTask.Comments.Single().OrganizationId.ShouldBe(office);
        persistedTask.Comments.Single().AuthorUserId.ShouldBe(user);
        persistedTask.Comments.Single().CreatedAt.ShouldNotBe(default);
        persistedTask.Comments.Single().CreatedBy.ShouldBe(Guid.Empty);
        persistedEvent.LegalCaseId.ShouldBeNull();
        persistedEvent.OrganizationId.ShouldBe(office);
        persistedEvent.CreatedAt.ShouldNotBe(default);
        persistedEvent.CreatedBy.ShouldBe(Guid.Empty);
    }

    private static async Task AssertRlsIsolationAsync(SchedulingDbContext db, Guid office, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await db.Database.ExecuteSqlAsync($"SET ROLE lexflow_app", ct);
            (await db.Database.SqlQuery<Guid>($"SELECT platform.current_organization_id() AS \"Value\"").SingleAsync(ct)).ShouldBe(office);
            (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM scheduling.tasks").SingleAsync(ct)).ShouldBe(2);
            (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM scheduling.calendar_events").SingleAsync(ct)).ShouldBe(1);
            (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM scheduling.task_checklist_items").SingleAsync(ct)).ShouldBe(1);
            (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM scheduling.task_comments").SingleAsync(ct)).ShouldBe(1);

            using (AmbientContext.Begin(Guid.CreateVersion7(), "agenda-other-tenant"))
            {
                (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM scheduling.tasks").SingleAsync(ct)).ShouldBe(0);
                (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM scheduling.calendar_events").SingleAsync(ct)).ShouldBe(0);
                (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM scheduling.task_checklist_items").SingleAsync(ct)).ShouldBe(0);
                (await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM scheduling.task_comments").SingleAsync(ct)).ShouldBe(0);
            }
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"RESET ROLE", CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }

    private sealed record LegacyTask(Guid Id, Guid CaseId, Guid PublicationId, Guid AssigneeId, DateOnly DueOn,
        DateTimeOffset ConfirmedAt, string Title)
    {
        public Guid ConfirmedBy => AssigneeId;
    }
}

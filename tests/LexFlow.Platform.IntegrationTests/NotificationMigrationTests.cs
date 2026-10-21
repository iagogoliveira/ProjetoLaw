using LexFlow.Platform.Domain.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LexFlow.Platform.IntegrationTests;

public sealed class NotificationMigrationTests(PlatformDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upgrade_preserves_notifications_and_isolates_all_new_tables_with_RLS()
    {
        var connection = await fixture.CreateEmptyDatabaseAsync();
        var office = Guid.CreateVersion7();
        var other = Guid.CreateVersion7();
        var tenant = new TestTenant { OrganizationId = office };
        await using var provider = PlatformDatabaseFixture.BuildProvider(connection, tenant, new TestUser { UserId = Guid.CreateVersion7() });
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var migrations = db.Database.GetMigrations().ToArray();
            await db.GetService<IMigrator>().MigrateAsync(migrations[^2], Ct);
            var notification = Notification.Create("upgrade", "publications.received", Guid.CreateVersion7(), null,
                new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero));
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(Ct);
            await db.Database.MigrateAsync(Ct);
            (await db.Notifications.SingleAsync(Ct)).Key.ShouldBe("upgrade");
            (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        }
        await AddSettingsAsync(provider);
        tenant.OrganizationId = other;
        await AddSettingsAsync(provider);
        await AssertIsolationAsync(connection, office, other);
    }

    private static async Task AddSettingsAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = Guid.CreateVersion7();
        db.NotificationPreferences.Add(NotificationPreference.Create(user));
        db.NotificationRules.Add(NotificationRule.Create());
        db.DigestDeliveries.Add(DigestDelivery.Create(user, new DateOnly(2026, 10, 21)));
        await db.SaveChangesAsync(Ct);
    }

    private static async Task AssertIsolationAsync(string connectionString, Guid office, Guid other)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using (var role = new NpgsqlCommand("SET ROLE lexflow_app", connection)) { await role.ExecuteNonQueryAsync(Ct); }
        foreach (var selected in new[] { office, other, Guid.CreateVersion7() })
        {
            await using var setting = new NpgsqlCommand("SELECT set_config('app.organization_id', $1, false)", connection);
            setting.Parameters.AddWithValue(selected.ToString("D"));
            await setting.ExecuteScalarAsync(Ct);
            await using var counts = new NpgsqlCommand("""
                SELECT (SELECT count(*) FROM platform.notification_preferences),
                       (SELECT count(*) FROM platform.notification_rules),
                       (SELECT count(*) FROM platform.digest_deliveries)
                """, connection);
            await using var reader = await counts.ExecuteReaderAsync(Ct);
            (await reader.ReadAsync(Ct)).ShouldBeTrue();
            var expected = selected == office || selected == other ? 1L : 0L;
            for (var index = 0; index < 3; index++) { reader.GetInt64(index).ShouldBe(expected); }
        }
        await using var insert = new NpgsqlCommand("""
            INSERT INTO platform.digest_deliveries(id, organization_id, user_id, day)
            VALUES ($1, $2, $3, $4)
            """, connection);
        insert.Parameters.AddWithValue(Guid.CreateVersion7());
        insert.Parameters.AddWithValue(office);
        insert.Parameters.AddWithValue(Guid.CreateVersion7());
        insert.Parameters.AddWithValue(new DateOnly(2026, 10, 22));
        (await Should.ThrowAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(Ct))).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }
}

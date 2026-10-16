using System.Text.Json;
using LexFlow.Platform.Domain.Calendar;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace LexFlow.Platform.IntegrationTests;

/// <summary>
/// Critério de aceite da Etapa 1 com a migration real: <c>SaveChanges</c> gera linha em <c>audit.audit_logs</c> com
/// antes/depois, encadeada por SHA-256 (trigger) e append-only.
/// </summary>
public sealed class AuditLogTests(PlatformDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private const string InsertAuditSql = """
        INSERT INTO audit.audit_logs (id, organization_id, user_id, action, entity_type, entity_id, changes, occurred_at)
        VALUES (gen_random_uuid(), $1, NULL, 'Updated', 'Sample', $2, '{"x":{"old":1,"new":2}}', now())
        """;

    [Fact]
    public async Task Save_changes_writes_chained_audit_row_with_before_and_after()
    {
        var id = Guid.CreateVersion7();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));
        var user = new TestUser { UserId = Guid.CreateVersion7() };
        await using (var provider = PlatformDatabaseFixture.BuildProvider(fixture.ConnectionString, new TestTenant(), user, time))
        {
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                db.Holidays.Add(new Holiday(id, "Feriado de teste", HolidayRule.FixedDate, 3, 10, null, "Teste", 2026));
                await db.SaveChangesAsync(Ct);
            }

            time.Advance(TimeSpan.FromSeconds(1));
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                db.Holidays.Remove(await db.Holidays.SingleAsync(h => h.Id == id, Ct));
                await db.SaveChangesAsync(Ct);
            }
        }

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT action, changes::text, user_id, chain_position, previous_hash, hash FROM audit.audit_logs WHERE entity_id = $1 ORDER BY chain_position",
            connection);
        command.Parameters.AddWithValue(id.ToString());
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rows = new List<(string Action, JsonElement Changes, Guid UserId, long Position, byte[]? Previous, byte[] Hash)>();
        while (await reader.ReadAsync(Ct))
        {
            rows.Add((reader.GetString(0), JsonDocument.Parse(reader.GetString(1)).RootElement.Clone(), reader.GetGuid(2), reader.GetInt64(3),
                reader.IsDBNull(4) ? null : (byte[])reader[4], (byte[])reader[5]));
        }

        rows.Count.ShouldBe(2);
        rows[0].Action.ShouldBe("Created");
        rows[0].Changes.GetProperty("name").GetProperty("new").GetString().ShouldBe("Feriado de teste");
        rows[1].Action.ShouldBe("Deleted");
        rows[1].Changes.GetProperty("name").GetProperty("old").GetString().ShouldBe("Feriado de teste");
        rows.ShouldAllBe(r => r.UserId == user.UserId);
        rows[0].Hash.Length.ShouldBe(32);
        rows[1].Position.ShouldBeGreaterThan(rows[0].Position);
        (await fixture.ScalarAsync<long?>("SELECT audit.verify_chain(NULL)")).ShouldBeNull();
    }

    [Fact]
    public async Task Concurrent_inserts_produce_a_gapless_valid_chain_per_organization()
    {
        var organization = Guid.CreateVersion7();

        await Task.WhenAll(Enumerable.Range(0, 25).Select(i => fixture.ExecuteAsync(InsertAuditSql, organization, $"entity-{i}")));

        (await fixture.ScalarAsync<long>("SELECT max(chain_position) FROM audit.audit_logs WHERE organization_id = $1", organization)).ShouldBe(25);
        (await fixture.ScalarAsync<long>("SELECT count(DISTINCT chain_position) FROM audit.audit_logs WHERE organization_id = $1", organization)).ShouldBe(25);
        (await fixture.ScalarAsync<long?>("SELECT audit.verify_chain($1)", organization)).ShouldBeNull();
    }

    [Fact]
    public async Task Each_organization_has_its_own_chain()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();

        await fixture.ExecuteAsync(InsertAuditSql, a, "1");
        await fixture.ExecuteAsync(InsertAuditSql, b, "1");
        await fixture.ExecuteAsync(InsertAuditSql, a, "2");

        (await fixture.ScalarAsync<long>("SELECT max(chain_position) FROM audit.audit_logs WHERE organization_id = $1", a)).ShouldBe(2);
        (await fixture.ScalarAsync<long>("SELECT max(chain_position) FROM audit.audit_logs WHERE organization_id = $1", b)).ShouldBe(1);
    }

    [Theory]
    [InlineData("UPDATE audit.audit_logs SET action = 'Adulterado' WHERE organization_id = $1")]
    [InlineData("DELETE FROM audit.audit_logs WHERE organization_id = $1")]
    public async Task Audit_rows_cannot_be_updated_or_deleted(string sql)
    {
        var organization = Guid.CreateVersion7();
        await fixture.ExecuteAsync(InsertAuditSql, organization, "1");

        var error = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync(sql, organization));

        error.MessageText.ShouldContain("append-only");
    }

    [Fact]
    public async Task Audit_table_cannot_be_truncated()
    {
        var error = await Should.ThrowAsync<PostgresException>(() => fixture.ExecuteAsync("TRUNCATE audit.audit_logs"));

        error.MessageText.ShouldContain("append-only");
    }

    [Fact]
    public async Task Tampering_with_a_row_is_detected_by_the_chain()
    {
        var organization = Guid.CreateVersion7();
        for (var i = 1; i <= 3; i++)
        {
            await fixture.ExecuteAsync(InsertAuditSql, organization, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // Simula um superusuário que desliga a proteção e altera o passado.
        await fixture.ExecuteAsync("ALTER TABLE audit.audit_logs DISABLE TRIGGER audit_logs_append_only");
        await fixture.ExecuteAsync(
            "UPDATE audit.audit_logs SET changes = '{\"x\":{\"old\":1,\"new\":999}}' WHERE organization_id = $1 AND chain_position = 2",
            organization);
        await fixture.ExecuteAsync("ALTER TABLE audit.audit_logs ENABLE TRIGGER audit_logs_append_only");

        (await fixture.ScalarAsync<long?>("SELECT audit.verify_chain($1)", organization)).ShouldBe(2);
    }
}

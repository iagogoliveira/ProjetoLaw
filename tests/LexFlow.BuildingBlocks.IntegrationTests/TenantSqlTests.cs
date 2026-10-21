using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Sample.Domain;
using LexFlow.Sample.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.IntegrationTests;

public sealed class TenantSqlTests(PersistenceFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Query_without_tenant_throws()
    {
        var tenant = new TestTenant();
        await using var provider = fixture.BuildProvider(tenant, new TestUser(), PersistenceFixture.NewClock());
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();

        await Should.ThrowAsync<InvalidOperationException>(() => TenantSql.QueryAsync<string>(db, tenant,
            "SELECT 'value'", new { }, Ct));
    }

    [Fact]
    public async Task Query_passes_quoted_text_and_sql_as_parameter_data()
    {
        var tenant = new TestTenant { OrganizationId = Guid.CreateVersion7() };
        await using var provider = fixture.BuildProvider(tenant, new TestUser(), PersistenceFixture.NewClock());
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        const string input = "texto com 'aspas'; DROP TABLE sample.matters; --";

        var result = await TenantSql.QueryAsync<ValueRow>(db, tenant, "SELECT @Input AS \"Value\"", new { Input = input }, Ct);

        result.ShouldHaveSingleItem().Value.ShouldBe(input);
    }

    [Fact]
    public async Task Query_keeps_external_connection_open_and_refreshes_tenant_setting()
    {
        var tenant = new TestTenant { OrganizationId = Guid.CreateVersion7() };
        await using var provider = fixture.BuildProvider(tenant, new TestUser(), PersistenceFixture.NewClock());
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        await db.Database.OpenConnectionAsync(Ct);

        try
        {
            var firstOrganization = tenant.OrganizationId.Value;
            var first = await TenantSql.QueryAsync<SettingRow>(db, tenant,
                "SELECT current_setting('app.organization_id') AS \"Value\"", new { }, Ct);
            first.ShouldHaveSingleItem().Value.ShouldBe(firstOrganization.ToString("D"));
            db.Database.GetDbConnection().State.ShouldBe(System.Data.ConnectionState.Open);

            tenant.OrganizationId = Guid.CreateVersion7();
            var secondOrganization = tenant.OrganizationId.Value;
            var second = await TenantSql.QueryAsync<SettingRow>(db, tenant,
                "SELECT current_setting('app.organization_id') AS \"Value\"", new { }, Ct);
            second.ShouldHaveSingleItem().Value.ShouldBe(secondOrganization.ToString("D"));
            db.Database.GetDbConnection().State.ShouldBe(System.Data.ConnectionState.Open);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task Query_uses_current_transaction_and_rollback_removes_only_transactional_insert()
    {
        var tenant = new TestTenant { OrganizationId = Guid.CreateVersion7() };
        var user = new TestUser { UserId = Guid.CreateVersion7() };
        await using var provider = fixture.BuildProvider(tenant, user, PersistenceFixture.NewClock());
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var committedId = Guid.CreateVersion7();
        var rolledBackId = Guid.CreateVersion7();
        db.Matters.Add(new Matter(committedId, "Committed matter", null));
        await db.SaveChangesAsync(Ct);

        await using (var transaction = await db.Database.BeginTransactionAsync(Ct))
        {
            db.Matters.Add(new Matter(rolledBackId, "Transactional matter", null));
            await db.SaveChangesAsync(Ct);

            var rows = await TenantSql.QueryAsync<MatterRow>(db, tenant,
                "SELECT id AS \"Id\" FROM sample.matters WHERE id = ANY(@Ids) ORDER BY id",
                new { Ids = new[] { committedId, rolledBackId } }, Ct);
            rows.Count.ShouldBe(2);
            rows.Select(row => row.Id).ShouldContain(committedId);
            rows.Select(row => row.Id).ShouldContain(rolledBackId);

            await transaction.RollbackAsync(Ct);
        }

        var afterRollback = await TenantSql.QueryAsync<MatterRow>(db, tenant,
            "SELECT id AS \"Id\" FROM sample.matters WHERE id = ANY(@Ids)",
            new { Ids = new[] { committedId, rolledBackId } }, Ct);
        afterRollback.Select(row => row.Id).ShouldBe([committedId]);
    }

    private sealed record ValueRow(string Value);

    private sealed record SettingRow(string Value);

    private sealed record MatterRow(Guid Id);
}

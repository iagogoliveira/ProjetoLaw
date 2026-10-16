using LexFlow.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Identity.IntegrationTests;

/// <summary>Migrations do Identity do zero, reaplicadas e revertidas (definição de pronto, seção 1.4).</summary>
public sealed class MigrationTests(IdentityDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_are_applied_from_scratch_and_reapplying_is_a_no_op()
    {
        await using var provider = IdentityDatabaseFixture.BuildProvider(fixture.ConnectionString, new FakeEmailSender());
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();

        await db.Database.MigrateAsync(Ct);

        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("users")]
    [InlineData("user_claims")]
    [InlineData("user_logins")]
    [InlineData("user_tokens")]
    [InlineData("sessions")]
    [InlineData("data_protection_keys")]
    [InlineData("processed_integration_events")]
    public async Task Module_tables_live_in_the_identity_schema(string table) =>
        (await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'identity' AND table_name = $1", table)).ShouldBe(1);

    [Fact]
    public async Task Module_does_not_create_the_audit_trail_nor_wolverine_tables()
    {
        var tables = await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'identity' AND table_name IN ('audit_logs', 'wolverine_outgoing_envelopes')");

        tables.ShouldBe(0);
    }

    [Fact]
    public async Task Migrations_can_be_reverted_and_reapplied()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await IdentityDatabaseFixture.MigrateAsync(connectionString);
        await using var provider = IdentityDatabaseFixture.BuildProvider(connectionString, new FakeEmailSender());
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        var migrator = db.GetService<IMigrator>();

        await migrator.MigrateAsync(Migration.InitialDatabase, cancellationToken: Ct);
        (await db.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
        await migrator.MigrateAsync(cancellationToken: Ct);

        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
    }
}

using LexFlow.Api.IntegrationTests;
using LexFlow.Crm.Infrastructure.Persistence;
using LexFlow.Identity.Infrastructure.Persistence;
using LexFlow.Litigation.Infrastructure.Persistence;
using LexFlow.Organization.Infrastructure.Persistence;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Publications.Infrastructure.Persistence;
using LexFlow.Scheduling.Infrastructure.Persistence;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LexFlow.SecurityTests;

/// <summary>
/// Row-Level Security e privilégios do banco (seção 6.2, item 5), testados direto no PostgreSQL com o login da
/// aplicação: mesmo SQL cru, sem o filtro do EF, fica preso à organização da sessão.
/// </summary>
public sealed class RowLevelSecurityTests(DependenciesFixture dependencies)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private async Task<NpgsqlConnection> OpenAsApplicationAsync(Guid? organization)
    {
        var connection = new NpgsqlConnection(dependencies.ApplicationConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT set_config('app.organization_id', $1, false)", connection);
        command.Parameters.AddWithValue(organization?.ToString() ?? string.Empty);
        await command.ExecuteNonQueryAsync(Ct);
        return connection;
    }

    private static async Task<List<Guid>> OrganizationsInAsync(NpgsqlConnection connection, string table)
    {
        await using var command = new NpgsqlCommand($"SELECT DISTINCT organization_id FROM {table}", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var organizations = new List<Guid>();
        while (await reader.ReadAsync(Ct))
        {
            organizations.Add(reader.GetGuid(0));
        }

        return organizations;
    }

    /// <summary>Todas as tabelas <see cref="ITenantOwned"/> de todos os módulos, lidas do modelo do EF.</summary>
    [Fact]
    public async Task Every_tenant_table_has_forced_row_level_security_with_the_tenant_policy()
    {
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        DbContext[] contexts =
        [
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>(),
            scope.ServiceProvider.GetRequiredService<OrganizationDbContext>(),
            scope.ServiceProvider.GetRequiredService<CrmDbContext>(),
            scope.ServiceProvider.GetRequiredService<LitigationDbContext>(),
            scope.ServiceProvider.GetRequiredService<PublicationsDbContext>(),
            scope.ServiceProvider.GetRequiredService<SchedulingDbContext>(),
        ];
        var tables = contexts
            .SelectMany(c => c.Model.GetEntityTypes())
            .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType))
            .Select(e => $"{e.GetSchema()}.{e.GetTableName()}")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await using var connection = new NpgsqlConnection(dependencies.AdminConnectionString);
        await connection.OpenAsync(Ct);
        var missing = new List<string>();
        foreach (var table in tables)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT c.relrowsecurity AND c.relforcerowsecurity
                    AND EXISTS (SELECT FROM pg_policies p WHERE p.schemaname = n.nspname AND p.tablename = c.relname AND p.policyname = 'tenant_isolation')
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname || '.' || c.relname = $1
                """,
                connection);
            command.Parameters.AddWithValue(table);
            if (await command.ExecuteScalarAsync(Ct) is not true)
            {
                missing.Add(table);
            }
        }

        tables.ShouldNotBeEmpty();
        missing.ShouldBeEmpty();
    }

    /// <summary>Critério de aceite da Etapa 3: SQL cru com <c>app.organization_id</c> da Org A não retorna linhas da Org B.</summary>
    [Fact]
    public async Task Raw_sql_as_the_application_only_sees_the_organization_of_the_session()
    {
        var first = await TestOrganizations.CreateWithMemberAsync(dependencies, "cases.read");
        var second = await TestOrganizations.CreateWithMemberAsync(dependencies, "cases.read");

        await using var asFirst = await OpenAsApplicationAsync(first.OrganizationId);
        await using var withoutTenant = await OpenAsApplicationAsync(null);

        (await OrganizationsInAsync(asFirst, "org.roles")).ShouldBe([first.OrganizationId]);
        (await OrganizationsInAsync(asFirst, "org.members")).ShouldBe([first.OrganizationId]);
        (await OrganizationsInAsync(asFirst, "org.role_permissions")).ShouldNotContain(second.OrganizationId);
        (await OrganizationsInAsync(withoutTenant, "org.roles")).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_application_cannot_write_rows_of_another_organization()
    {
        var first = await TestOrganizations.CreateWithMemberAsync(dependencies);
        var second = await TestOrganizations.CreateWithMemberAsync(dependencies);
        await using var asFirst = await OpenAsApplicationAsync(first.OrganizationId);

        await using var insert = new NpgsqlCommand(
            "INSERT INTO org.practice_areas (id, organization_id, name, created_at, created_by, is_deleted) VALUES ($1, $2, 'Invasão', now(), $3, false)",
            asFirst);
        insert.Parameters.AddWithValue(Guid.CreateVersion7());
        insert.Parameters.AddWithValue(second.OrganizationId);
        insert.Parameters.AddWithValue(first.UserId);
        await using var update = new NpgsqlCommand("UPDATE org.roles SET name = 'Tomado' WHERE organization_id = $1", asFirst);
        update.Parameters.AddWithValue(second.OrganizationId);

        (await Should.ThrowAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(Ct))).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await update.ExecuteNonQueryAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task The_audit_trail_is_append_only_for_the_application()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies);
        await using var connection = await OpenAsApplicationAsync(member.OrganizationId);

        await using var update = new NpgsqlCommand("UPDATE audit.audit_logs SET action = 'Adulterado'", connection);
        await using var delete = new NpgsqlCommand("DELETE FROM audit.audit_logs", connection);

        (await Should.ThrowAsync<PostgresException>(() => update.ExecuteNonQueryAsync(Ct))).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await Should.ThrowAsync<PostgresException>(() => delete.ExecuteNonQueryAsync(Ct))).SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
        (await OrganizationsInAsync(connection, "audit.audit_logs")).ShouldBe([member.OrganizationId]);
    }

    [Fact]
    public async Task The_application_login_has_no_special_powers()
    {
        await using var connection = new NpgsqlConnection(dependencies.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(
            "SELECT rolsuper OR rolbypassrls OR rolcreaterole OR rolcreatedb FROM pg_roles WHERE rolname = $1", connection);
        command.Parameters.AddWithValue(DependenciesFixture.ApplicationLoginName);

        (await command.ExecuteScalarAsync(Ct)).ShouldBe(false);
    }
}

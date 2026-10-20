using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Members;
using LexFlow.Organization.Domain.OrgStructure;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Organization.Infrastructure;
using LexFlow.Organization.Infrastructure.Persistence;
using LexFlow.Platform.Infrastructure;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using OrganizationEntity = LexFlow.Organization.Domain.Organizations.Organization;

[assembly: AssemblyFixture(typeof(LexFlow.Organization.IntegrationTests.OrganizationDatabaseFixture))]

namespace LexFlow.Organization.IntegrationTests;

/// <summary>PostgreSQL com as migrations reais do Platform (trilha de auditoria) e do Organization aplicadas do zero.</summary>
public sealed class OrganizationDatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("pgvector/pgvector:pg17-trixie").Build();

    public string ConnectionString => Postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await Postgres.StartAsync();
        await MigrateAsync(ConnectionString);
    }

    public static async Task MigrateAsync(string connectionString)
    {
        await using var provider = BuildProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync();
    }

    public static ServiceProvider BuildProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { ["ConnectionStrings:lexflow"] = connectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSystemContexts();
        services.AddPlatformInfrastructure(configuration);
        services.AddOrganizationInfrastructure(configuration);
        return services.BuildServiceProvider(validateScopes: true);
    }

    public static Task<int> SeedLegacyOrganizationAsync(
        OrganizationDbContext db,
        Guid id,
        string name,
        IClock clock,
        CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO org.organizations (id, name, mfa_required, created_at, created_by) VALUES ({id}, {name}, false, {clock.UtcNow}, {Guid.Empty})",
            cancellationToken);

    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var name = "lexflow_" + Guid.CreateVersion7().ToString("N");
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public async ValueTask DisposeAsync() => await Postgres.DisposeAsync();
}

/// <summary>Migrations do módulo e as garantias do banco (unicidade dentro do tenant, ignorando excluídos).</summary>
public sealed class OrganizationDatabaseTests(OrganizationDatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    /// <summary>Organização nova com o tenant dela ativo, como no onboarding.</summary>
    private async Task<Guid> SeedOrganizationAsync(Action<OrganizationDbContext, Guid> seed)
    {
        var organizationId = Guid.CreateVersion7();
        using var ambient = AmbientContext.Begin(organizationId, "organization-tests");
        await using var provider = OrganizationDatabaseFixture.BuildProvider(fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        db.Organizations.Add(OrganizationEntity.Create(organizationId, "Escritório de Teste"));
        seed(db, organizationId);
        await db.SaveChangesAsync(Ct);
        return organizationId;
    }

    private async Task SaveAsync(Guid organizationId, Action<OrganizationDbContext> change)
    {
        using var ambient = AmbientContext.Begin(organizationId, "organization-tests");
        await using var provider = OrganizationDatabaseFixture.BuildProvider(fixture.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        change(db);
        await db.SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Migrations_apply_from_scratch_and_revert()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await OrganizationDatabaseFixture.MigrateAsync(connectionString);
        await using var provider = OrganizationDatabaseFixture.BuildProvider(connectionString);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        var migrator = db.GetService<IMigrator>();

        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
        await migrator.MigrateAsync(Migration.InitialDatabase, cancellationToken: Ct);
        (await db.Database.GetAppliedMigrationsAsync(Ct)).ShouldBeEmpty();
        await migrator.MigrateAsync(cancellationToken: Ct);
        (await db.Database.GetPendingMigrationsAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_user_belongs_once_to_each_organization()
    {
        var user = Guid.CreateVersion7();
        var organizationId = await SeedOrganizationAsync((db, org) => db.Members.Add(OrganizationMember.Join(Guid.CreateVersion7(), org, user, default)));

        var duplicate = await Should.ThrowAsync<DbUpdateException>(() =>
            SaveAsync(organizationId, db => db.Members.Add(OrganizationMember.Join(Guid.CreateVersion7(), organizationId, user, default))));

        duplicate.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Names_are_unique_inside_the_organization_but_free_across_organizations()
    {
        var first = await SeedOrganizationAsync((db, _) => db.PracticeAreas.Add(PracticeArea.Create(Guid.CreateVersion7(), "Trabalhista")));
        await SeedOrganizationAsync((db, _) => db.PracticeAreas.Add(PracticeArea.Create(Guid.CreateVersion7(), "Trabalhista")));

        await Should.ThrowAsync<DbUpdateException>(() =>
            SaveAsync(first, db => db.PracticeAreas.Add(PracticeArea.Create(Guid.CreateVersion7(), "Trabalhista"))));
    }

    [Fact]
    public async Task A_default_role_exists_once_per_organization()
    {
        var organizationId = await SeedOrganizationAsync((db, _) => db.Roles.Add(Role.Create(Guid.CreateVersion7(), "Administrador", systemKey: "office_admin")));

        await Should.ThrowAsync<DbUpdateException>(() =>
            SaveAsync(organizationId, db => db.Roles.Add(Role.Create(Guid.CreateVersion7(), "Outro administrador", systemKey: "office_admin"))));
        await SaveAsync(organizationId, db => db.Roles.Add(Role.Create(Guid.CreateVersion7(), "Perfil do escritório")));
    }

    /// <summary>Migration de dados do PR 3E: perfis padrão já existentes ganham <c>settings.custom_fields.read</c>.</summary>
    [Fact]
    public async Task Existing_default_roles_receive_the_custom_fields_read_permission()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var provider = OrganizationDatabaseFixture.BuildProvider(connectionString);
        await using (var setup = provider.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync(Ct);
            await setup.ServiceProvider.GetRequiredService<OrganizationDbContext>().GetService<IMigrator>().MigrateAsync("20261017133643_Invitations", cancellationToken: Ct);
        }

        var organizationId = Guid.CreateVersion7();
        var lawyer = Role.Create(Guid.CreateVersion7(), "Advogado", systemKey: "lawyer");
        lawyer.SetPermissions(["cases.read"]);
        var custom = Role.Create(Guid.CreateVersion7(), "Perfil do escritório");
        custom.SetPermissions(["cases.read"]);
        using (AmbientContext.Begin(organizationId, "organization-tests"))
        await using (var seed = provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            await OrganizationDatabaseFixture.SeedLegacyOrganizationAsync(db, organizationId, "Escritório antigo",
                seed.ServiceProvider.GetRequiredService<IClock>(), Ct);
            db.Roles.AddRange(lawyer, custom);
            await db.SaveChangesAsync(Ct);
        }

        await using (var upgrade = provider.CreateAsyncScope())
        {
            await upgrade.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync(Ct);
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT role_id FROM org.role_permissions WHERE permission = 'settings.custom_fields.read'", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var granted = new List<Guid>();
        while (await reader.ReadAsync(Ct))
        {
            granted.Add(reader.GetGuid(0));
        }

        granted.ShouldBe([lawyer.Id]);
    }

    /// <summary>Migration de dados do PR 4B: só Administrador do escritório e Sócio ganham <c>crm.people.anonymize</c>.</summary>
    [Fact]
    public async Task Existing_admin_and_partner_roles_receive_the_anonymize_permission()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var provider = OrganizationDatabaseFixture.BuildProvider(connectionString);
        await using (var setup = provider.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync(Ct);
            await setup.ServiceProvider.GetRequiredService<OrganizationDbContext>().GetService<IMigrator>().MigrateAsync("20261017152626_CustomFieldsReadPermission", cancellationToken: Ct);
        }

        var organizationId = Guid.CreateVersion7();
        var admin = Role.Create(Guid.CreateVersion7(), "Administrador", systemKey: "office_admin");
        var partner = Role.Create(Guid.CreateVersion7(), "Sócio", systemKey: "partner");
        var lawyer = Role.Create(Guid.CreateVersion7(), "Advogado", systemKey: "lawyer");
        using (AmbientContext.Begin(organizationId, "organization-tests"))
        await using (var seed = provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            await OrganizationDatabaseFixture.SeedLegacyOrganizationAsync(db, organizationId, "Escritório antigo",
                seed.ServiceProvider.GetRequiredService<IClock>(), Ct);
            db.Roles.AddRange(admin, partner, lawyer);
            await db.SaveChangesAsync(Ct);
        }

        await using (var upgrade = provider.CreateAsyncScope())
        {
            await upgrade.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync(Ct);
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT role_id FROM org.role_permissions WHERE permission = 'crm.people.anonymize'", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var granted = new List<Guid>();
        while (await reader.ReadAsync(Ct))
        {
            granted.Add(reader.GetGuid(0));
        }

        granted.ShouldBe([admin.Id, partner.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Deadline_review_migration_preserves_organization_and_defaults_policy_to_false()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var provider = OrganizationDatabaseFixture.BuildProvider(connectionString);
        await using (var setup = provider.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync(Ct);
            await setup.ServiceProvider.GetRequiredService<OrganizationDbContext>().GetService<IMigrator>()
                .MigrateAsync("20261020185814_ScheduleCalendarPermission", cancellationToken: Ct);
        }

        var organizationId = Guid.CreateVersion7();
        const string organizationName = "Escritório antes da política de conferência";
        using (AmbientContext.Begin(organizationId, "legacy-deadline-review"))
        await using (var seed = provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            await OrganizationDatabaseFixture.SeedLegacyOrganizationAsync(db, organizationId, organizationName,
                seed.ServiceProvider.GetRequiredService<IClock>(), Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE org.organizations SET mfa_required = true WHERE id = {organizationId}", Ct);
        }

        await using (var upgrade = provider.CreateAsyncScope())
        {
            await upgrade.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync(Ct);
        }

        using (AmbientContext.Begin(organizationId, "deadline-review-default"))
        await using (var verifyDefault = provider.CreateAsyncScope())
        {
            var db = verifyDefault.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var organization = await db.Organizations.SingleAsync(o => o.Id == organizationId, Ct);
            organization.Id.ShouldBe(organizationId);
            organization.Name.ShouldBe(organizationName);
            organization.MfaRequired.ShouldBeTrue();
            organization.RequireDeadlineReview.ShouldBeFalse();
            organization.SetDeadlineReviewRequired(true);
            await db.SaveChangesAsync(Ct);
        }

        using (AmbientContext.Begin(organizationId, "deadline-review-persisted"))
        await using (var verifyPersisted = provider.CreateAsyncScope())
        {
            var organization = await verifyPersisted.ServiceProvider.GetRequiredService<OrganizationDbContext>()
                .Organizations.AsNoTracking().SingleAsync(o => o.Id == organizationId, Ct);
            organization.RequireDeadlineReview.ShouldBeTrue();
        }
    }
}

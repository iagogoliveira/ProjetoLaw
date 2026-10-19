using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Organization.Infrastructure.Persistence;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrganizationEntity = LexFlow.Organization.Domain.Organizations.Organization;

namespace LexFlow.Organization.IntegrationTests;

public sealed class PublicationClosePermissionTests(OrganizationDatabaseFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Existing_admin_partner_and_controller_receive_close_but_custom_and_lawyer_do_not()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        await using var provider = OrganizationDatabaseFixture.BuildProvider(connectionString);
        await using (var setup = provider.CreateAsyncScope())
        {
            await setup.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync(Ct);
            await setup.ServiceProvider.GetRequiredService<OrganizationDbContext>().GetService<IMigrator>()
                .MigrateAsync("20261018215018_RiskAssessPermission", cancellationToken: Ct);
        }
        var office = Guid.CreateVersion7();
        var admin = Role.Create(Guid.CreateVersion7(), "Administrador", systemKey: "office_admin");
        var partner = Role.Create(Guid.CreateVersion7(), "Sócio", systemKey: "partner");
        var controller = Role.Create(Guid.CreateVersion7(), "Controladoria", systemKey: "legal_controller");
        var custom = Role.Create(Guid.CreateVersion7(), "Perfil do escritório");
        var lawyer = Role.Create(Guid.CreateVersion7(), "Advogado", systemKey: "lawyer");
        using (AmbientContext.Begin(office, "legacy-roles"))
        await using (var seed = provider.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            db.Organizations.Add(OrganizationEntity.Create(office, "Escritório anterior ao 6D"));
            db.Roles.AddRange(admin, partner, controller, custom, lawyer);
            await db.SaveChangesAsync(Ct);
        }
        await using (var upgrade = provider.CreateAsyncScope())
        {
            await upgrade.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync(Ct);
        }
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT role_id FROM org.role_permissions WHERE permission = 'publications.close'", connection);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var granted = new List<Guid>();
        while (await reader.ReadAsync(Ct)) { granted.Add(reader.GetGuid(0)); }
        granted.ShouldBe([admin.Id, partner.Id, controller.Id], ignoreOrder: true);
    }
}

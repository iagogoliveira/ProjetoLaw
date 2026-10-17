using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Domain.Members;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Organization.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OrganizationEntity = LexFlow.Organization.Domain.Organizations.Organization;

namespace LexFlow.Api.IntegrationTests;

internal sealed record SeededMember(Guid OrganizationId, Guid UserId, Guid MemberId, Guid RoleId);

/// <summary>Dados de organização direto no banco, para testes que precisam de um membro com permissões exatas.</summary>
internal static class TestOrganizations
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Organização nova com um membro cujo único perfil tem exatamente <paramref name="permissions"/>.</summary>
    public static async Task<SeededMember> CreateWithMemberAsync(DependenciesFixture dependencies, params string[] permissions)
    {
        var organizationId = Guid.CreateVersion7();
        await RunAsync(dependencies, organizationId, db => db.Organizations.Add(OrganizationEntity.Create(organizationId, "Escritório de Teste")));
        return await AddMemberAsync(dependencies, organizationId, Guid.CreateVersion7(), permissions);
    }

    /// <summary>Novo membro numa organização existente, com um perfil próprio.</summary>
    public static async Task<SeededMember> AddMemberAsync(DependenciesFixture dependencies, Guid organizationId, Guid userId, params string[] permissions)
    {
        var member = OrganizationMember.Join(Guid.CreateVersion7(), organizationId, userId, new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        var role = Role.Create(Guid.CreateVersion7(), $"Perfil {Guid.CreateVersion7():N}");
        role.SetPermissions(permissions);
        await RunAsync(dependencies, organizationId, db =>
        {
            db.Roles.Add(role);
            db.Members.Add(member);
            db.UserOrganizations.Add(new UserOrganization(userId, organizationId));
            db.RoleAssignments.Add(MemberRoleAssignment.Assign(Guid.CreateVersion7(), member.Id, role.Id, DataScope.Organization).Value);
        });
        return new SeededMember(organizationId, userId, member.Id, role.Id);
    }

    private static async Task RunAsync(DependenciesFixture dependencies, Guid organizationId, Action<OrganizationDbContext> change)
    {
        using var ambient = AmbientContext.Begin(organizationId, "test-organizations");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        change(db);
        await db.SaveChangesAsync(Ct);
        var cache = scope.ServiceProvider.GetRequiredService<IAccessCache>();
        foreach (var user in db.ChangeTracker.Entries<UserOrganization>().Select(e => e.Entity.UserId))
        {
            await cache.InvalidateMembershipsAsync(user, Ct);
        }

        await cache.InvalidateOrganizationAsync(organizationId, Ct);
    }
}

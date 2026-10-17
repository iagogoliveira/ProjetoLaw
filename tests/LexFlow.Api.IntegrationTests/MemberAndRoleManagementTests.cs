using System.Net;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts.Permissions;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Perfis editáveis (criar, renomear, excluir), catálogo para a matriz e remoção de membros (Etapa 3, PR 3D).</summary>
public sealed class MemberAndRoleManagementTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly string[] CasesRead = ["cases.read"];
    private static readonly string[] Universal = [PermissionCatalog.InvitationsAccept];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;
    private OrganizationApi _api = null!;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        _api = new OrganizationApi(_client);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Office_creates_renames_and_deletes_its_own_roles()
    {
        var (organization, admin) = await _api.OnboardAsync();

        var (createdStatus, created) = await _api.SendAsync(HttpMethod.Post, "/api/v1/roles", admin, organization, new { name = "Correspondente interno", description = "Diligências", permissions = CasesRead });
        var role = created.GetProperty("id").GetGuid();
        var (duplicateStatus, duplicate) = await _api.SendAsync(HttpMethod.Post, "/api/v1/roles", admin, organization, new { name = "Correspondente interno", permissions = CasesRead });
        var (universalStatus, _) = await _api.SendAsync(HttpMethod.Post, "/api/v1/roles", admin, organization, new { name = "Universal", permissions = Universal });
        var (renameStatus, renamed) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/roles/{role}", admin, organization, new { name = "Diligências", description = (string?)null });
        var (deleteStatus, _) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/roles/{role}", admin, organization);

        createdStatus.ShouldBe(HttpStatusCode.Created);
        created.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldBe(CasesRead);
        created.GetProperty("systemKey").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        duplicateStatus.ShouldBe(HttpStatusCode.Conflict);
        OrganizationApi.Code(duplicate).ShouldBe("organization.name_taken");
        universalStatus.ShouldBe(HttpStatusCode.BadRequest);
        renameStatus.ShouldBe(HttpStatusCode.OK);
        renamed.GetProperty("name").GetString().ShouldBe("Diligências");
        renamed.GetProperty("permissions").GetArrayLength().ShouldBe(1);
        deleteStatus.ShouldBe(HttpStatusCode.NoContent);
        (await _api.SendAsync(HttpMethod.Get, "/api/v1/roles", admin, organization)).Body.EnumerateArray()
            .ShouldNotContain(r => r.GetProperty("id").GetGuid() == role);
        (await _api.SendAsync(HttpMethod.Post, "/api/v1/roles", admin, organization, new { name = "Correspondente interno", permissions = CasesRead }))
            .Status.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Roles_in_use_and_the_admin_role_cannot_be_deleted()
    {
        var (organization, admin) = await _api.OnboardAsync();
        var adminRole = await _api.RoleIdAsync(organization, admin, DefaultRoles.OfficeAdmin);
        var member = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7(), "cases.read");
        var invitedRole = await _api.RoleIdAsync(organization, admin, DefaultRoles.Paralegal);
        (await _api.SendAsync(HttpMethod.Post, "/api/v1/organization/invitations", admin, organization, new
        {
            email = TestAccounts.NewEmail(),
            roles = new[] { new { roleId = invitedRole, scope = "own", scopeRefId = (Guid?)null } },
        })).Status.ShouldBe(HttpStatusCode.Created);

        var (adminStatus, adminProblem) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/roles/{adminRole}", admin, organization);
        var (assignedStatus, assigned) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/roles/{member.RoleId}", admin, organization);
        var (invitedStatus, _) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/roles/{invitedRole}", admin, organization);
        var (missingStatus, _) = await _api.SendAsync(HttpMethod.Put, $"/api/v1/roles/{Guid.CreateVersion7()}", admin, organization, new { name = "Nada" });

        adminStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(adminProblem).ShouldBe("organization.admin_role_locked");
        assignedStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(assigned).ShouldBe("organization.role_in_use");
        invitedStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        missingStatus.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Permission_catalog_lists_only_assignable_permissions_by_group()
    {
        var (organization, admin) = await _api.OnboardAsync();

        var (status, groups) = await _api.SendAsync(HttpMethod.Get, "/api/v1/permissions", admin, organization);

        status.ShouldBe(HttpStatusCode.OK);
        var names = groups.EnumerateArray().SelectMany(g => g.GetProperty("permissions").EnumerateArray()).Select(p => p.GetProperty("name").GetString()).ToList();
        names.ShouldBe(PermissionCatalog.Assignable.Select(p => p.Name));
        groups[0].GetProperty("group").GetString().ShouldBe(PermissionCatalog.Groups.Settings);
    }

    [Fact]
    public async Task Removed_member_loses_access_and_the_last_admin_stays()
    {
        var (organization, admin) = await _api.OnboardAsync();
        var adminMember = (await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/members", admin, organization)).Body
            .EnumerateArray().Single().GetProperty("id").GetGuid();
        var member = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7(), "cases.read");
        (await _api.SendAsync(HttpMethod.Get, "/api/v1/me/access", member.UserId, organization)).Status.ShouldBe(HttpStatusCode.OK);

        var (removeStatus, _) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/organization/members/{member.MemberId}", admin, organization);
        var (afterStatus, after) = await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/members", member.UserId, organization);
        var (lastAdminStatus, lastAdmin) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/organization/members/{adminMember}", admin, organization);
        var (againStatus, _) = await _api.SendAsync(HttpMethod.Delete, $"/api/v1/organization/members/{member.MemberId}", admin, organization);

        removeStatus.ShouldBe(HttpStatusCode.NoContent);
        afterStatus.ShouldBe(HttpStatusCode.Forbidden);
        OrganizationApi.Code(after).ShouldBe("organization.not_a_member");
        lastAdminStatus.ShouldBe(HttpStatusCode.UnprocessableEntity);
        OrganizationApi.Code(lastAdmin).ShouldBe("organization.last_admin");
        againStatus.ShouldBe(HttpStatusCode.NotFound);
        (await _api.SendAsync(HttpMethod.Get, "/api/v1/organization/members", admin, organization)).Body.GetArrayLength().ShouldBe(1);
    }
}

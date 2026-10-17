using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Web.Common.Security;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Tenant validado contra a pertença, permissões reais pelo perfil, troca de organização e MFA obrigatório por
/// organização (Etapa 3, PR 3B), pela API real com PostgreSQL e Redis.
/// </summary>
public sealed class AccessAndPermissionsTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly string[] CasesRead = ["cases.read"];
    private static readonly string[] UnknownPermission = ["cases.fly"];
    private static readonly string[] UniversalPermission = [PermissionCatalog.ProfileRead];

    private readonly LexFlowApiFactory _factory = new(dependencies.Settings);
    private HttpClient _client = null!;

    public ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> SendAsync(HttpMethod method, string path, Guid user, Guid? organization = null, object? body = null, string? amr = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestAuthenticationHandler.UserHeader, user.ToString());
        if (organization is { } org)
        {
            request.Headers.Add(TenantResolutionMiddleware.HeaderName, org.ToString());
        }

        if (amr is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.AmrHeader, amr);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _client.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }

    /// <summary>Escritório criado pelo onboarding real: o usuário devolvido é o Administrador do escritório.</summary>
    private async Task<(Guid Organization, Guid Admin)> OnboardAsync(string name = "Escritório")
    {
        var admin = Guid.CreateVersion7();
        var (status, body) = await SendAsync(HttpMethod.Post, "/api/v1/organizations", admin, body: new { name });
        status.ShouldBe(HttpStatusCode.Created);
        return (body.GetProperty("id").GetGuid(), admin);
    }

    private async Task<JsonElement> RolesAsync(Guid organization, Guid admin) =>
        (await SendAsync(HttpMethod.Get, "/api/v1/roles", admin, organization)).Body;

    private async Task<Guid> RoleIdAsync(Guid organization, Guid admin, string systemKey) =>
        (await RolesAsync(organization, admin)).EnumerateArray()
            .Single(r => r.GetProperty("systemKey").ValueKind == JsonValueKind.String && r.GetProperty("systemKey").GetString() == systemKey)
            .GetProperty("id").GetGuid();

    [Fact]
    public async Task Office_admin_lists_members_and_roles_of_the_active_organization()
    {
        var (organization, admin) = await OnboardAsync();

        var (status, members) = await SendAsync(HttpMethod.Get, "/api/v1/organization/members", admin, organization);

        status.ShouldBe(HttpStatusCode.OK);
        var member = members.EnumerateArray().Single();
        member.GetProperty("userId").GetGuid().ShouldBe(admin);
        member.GetProperty("roles")[0].GetProperty("scope").GetString().ShouldBe("organization");
        (await RolesAsync(organization, admin)).GetArrayLength().ShouldBe(DefaultRoles.All.Count);
    }

    [Fact]
    public async Task Requests_for_an_organization_the_user_is_not_part_of_are_refused()
    {
        var (organization, _) = await OnboardAsync();
        var (_, outsider) = await OnboardAsync("Outro escritório");

        var (status, body) = await SendAsync(HttpMethod.Get, "/api/v1/organization/members", outsider, organization);

        status.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("organization.not_a_member");
    }

    [Fact]
    public async Task Member_without_the_permission_gets_403()
    {
        var member = await TestOrganizations.CreateWithMemberAsync(dependencies, "cases.read");

        var (status, body) = await SendAsync(HttpMethod.Get, "/api/v1/organization/members", member.UserId, member.OrganizationId);

        status.ShouldBe(HttpStatusCode.Forbidden);
        body.GetProperty("code").GetString().ShouldBe("auth.forbidden");
    }

    /// <summary>Critério de aceite da Etapa 3: remover <c>settings.users.manage</c> de um perfil bloqueia a ação em até 5 s, sem novo login.</summary>
    [Fact]
    public async Task Removing_a_permission_from_a_role_blocks_the_action_within_5_seconds_without_a_new_login()
    {
        var (organization, admin) = await OnboardAsync();
        var manager = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7(), PermissionCatalog.SettingsUsersManage);
        var colleague = await TestOrganizations.AddMemberAsync(dependencies, organization, Guid.CreateVersion7(), "cases.read");
        var assign = new { roles = new[] { new { roleId = colleague.RoleId, scope = "organization", scopeRefId = (Guid?)null } } };
        (await SendAsync(HttpMethod.Put, $"/api/v1/organization/members/{colleague.MemberId}/roles", manager.UserId, organization, assign)).Status
            .ShouldBe(HttpStatusCode.NoContent);

        (await SendAsync(HttpMethod.Put, $"/api/v1/roles/{manager.RoleId}/permissions", admin, organization, new { permissions = CasesRead })).Status
            .ShouldBe(HttpStatusCode.NoContent);

        var elapsed = Stopwatch.StartNew();
        HttpStatusCode status;
        do
        {
            status = (await SendAsync(HttpMethod.Put, $"/api/v1/organization/members/{colleague.MemberId}/roles", manager.UserId, organization, assign)).Status;
        }
        while (status != HttpStatusCode.Forbidden && elapsed.Elapsed < TimeSpan.FromSeconds(5));

        status.ShouldBe(HttpStatusCode.Forbidden);
        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Role_permissions_must_come_from_the_catalog_and_the_admin_role_is_locked()
    {
        var (organization, admin) = await OnboardAsync();
        var lawyer = await RoleIdAsync(organization, admin, DefaultRoles.Lawyer);
        var adminRole = await RoleIdAsync(organization, admin, DefaultRoles.OfficeAdmin);

        var unknown = await SendAsync(HttpMethod.Put, $"/api/v1/roles/{lawyer}/permissions", admin, organization, new { permissions = UnknownPermission });
        var universal = await SendAsync(HttpMethod.Put, $"/api/v1/roles/{lawyer}/permissions", admin, organization, new { permissions = UniversalPermission });
        var locked = await SendAsync(HttpMethod.Put, $"/api/v1/roles/{adminRole}/permissions", admin, organization, new { permissions = CasesRead });

        unknown.Status.ShouldBe(HttpStatusCode.BadRequest);
        unknown.Body.GetProperty("errors").EnumerateObject().Single().Value[0].GetProperty("code").GetString().ShouldBe("organization.unknown_permission");
        universal.Status.ShouldBe(HttpStatusCode.BadRequest);
        locked.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        locked.Body.GetProperty("code").GetString().ShouldBe("organization.admin_role_locked");
    }

    [Fact]
    public async Task The_office_never_loses_its_last_admin_and_scopes_must_reference_its_structure()
    {
        var (organization, admin) = await OnboardAsync();
        var members = (await SendAsync(HttpMethod.Get, "/api/v1/organization/members", admin, organization)).Body;
        var adminMember = members[0].GetProperty("id").GetGuid();
        var lawyer = await RoleIdAsync(organization, admin, DefaultRoles.Lawyer);
        string Path() => $"/api/v1/organization/members/{adminMember}/roles";

        var lastAdmin = await SendAsync(HttpMethod.Put, Path(), admin, organization, new { roles = new[] { new { roleId = lawyer, scope = "organization", scopeRefId = (Guid?)null } } });
        var missingTeam = await SendAsync(HttpMethod.Put, Path(), admin, organization, new { roles = new[] { new { roleId = lawyer, scope = "team", scopeRefId = (Guid?)null } } });
        var unknownTeam = await SendAsync(HttpMethod.Put, Path(), admin, organization, new { roles = new[] { new { roleId = lawyer, scope = "team", scopeRefId = (Guid?)Guid.CreateVersion7() } } });

        lastAdmin.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        lastAdmin.Body.GetProperty("code").GetString().ShouldBe("organization.last_admin");
        missingTeam.Body.GetProperty("code").GetString().ShouldBe("organization.scope_reference_required");
        unknownTeam.Body.GetProperty("code").GetString().ShouldBe("organization.scope_reference_not_found");
    }

    [Fact]
    public async Task Required_mfa_blocks_office_data_until_the_token_has_a_second_factor()
    {
        var (organization, admin) = await OnboardAsync();

        (await SendAsync(HttpMethod.Put, "/api/v1/organization/security", admin, organization, new { mfaRequired = true })).Status.ShouldBe(HttpStatusCode.NoContent);
        var withoutMfa = await SendAsync(HttpMethod.Get, "/api/v1/organization/members", admin, organization);
        var withMfa = await SendAsync(HttpMethod.Get, "/api/v1/organization/members", admin, organization, amr: "pwd,mfa");
        var access = await SendAsync(HttpMethod.Get, "/api/v1/me/access", admin, organization);

        withoutMfa.Status.ShouldBe(HttpStatusCode.Forbidden);
        withoutMfa.Body.GetProperty("code").GetString().ShouldBe("organization.mfa_required");
        withMfa.Status.ShouldBe(HttpStatusCode.OK);
        access.Status.ShouldBe(HttpStatusCode.OK);
        access.Body.GetProperty("mfaRequired").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Access_lists_organizations_and_permissions_and_the_user_can_switch_organization()
    {
        var (first, user) = await OnboardAsync("Primeiro");
        var (status, created) = await SendAsync(HttpMethod.Post, "/api/v1/organizations", user, body: new { name = "Segundo" });
        status.ShouldBe(HttpStatusCode.Created);
        var second = created.GetProperty("id").GetGuid();

        var ambiguous = (await SendAsync(HttpMethod.Get, "/api/v1/me/access", user)).Body;
        var explicitFirst = (await SendAsync(HttpMethod.Get, "/api/v1/me/access", user, first)).Body;
        (await SendAsync(HttpMethod.Post, "/api/v1/me/active-organization", user, body: new { organizationId = second })).Status.ShouldBe(HttpStatusCode.NoContent);
        var afterSwitch = (await SendAsync(HttpMethod.Get, "/api/v1/me/access", user)).Body;
        var notMember = await SendAsync(HttpMethod.Post, "/api/v1/me/active-organization", user, body: new { organizationId = Guid.CreateVersion7() });

        ambiguous.GetProperty("organizations").GetArrayLength().ShouldBe(2);
        ambiguous.GetProperty("activeOrganizationId").ValueKind.ShouldBe(JsonValueKind.Null);
        ambiguous.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldBe(
            [PermissionCatalog.ProfileManage, PermissionCatalog.ProfileRead, PermissionCatalog.OrganizationsCreate], ignoreOrder: true);
        explicitFirst.GetProperty("activeOrganizationId").GetGuid().ShouldBe(first);
        explicitFirst.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldContain(PermissionCatalog.SettingsRolesManage);
        afterSwitch.GetProperty("activeOrganizationId").GetGuid().ShouldBe(second);
        notMember.Status.ShouldBe(HttpStatusCode.Forbidden);
        notMember.Body.GetProperty("code").GetString().ShouldBe("organization.not_a_member");
    }
}

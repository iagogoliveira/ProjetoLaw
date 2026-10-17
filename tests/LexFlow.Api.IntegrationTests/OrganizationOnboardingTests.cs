using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Organization.Domain.Roles;
using LexFlow.Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Onboarding (Etapa 3, tarefa 3) pela API real: organização, perfis padrão e o criador como administrador.</summary>
public sealed class OrganizationOnboardingTests(DependenciesFixture dependencies) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

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

    private async Task<HttpResponseMessage> CreateAsync(Guid? userId, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/organizations") { Content = JsonContent.Create(new { name }) };
        if (userId is { } id)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeader, id.ToString());
        }

        return await _client.SendAsync(request, Ct);
    }

    private async Task<Guid> CreateOrganizationAsync(Guid userId, string name)
    {
        using var response = await CreateAsync(userId, name);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>Lê os dados com o tenant da organização dada, como um consumidor ou job faria.</summary>
    private async Task<T> AsTenantAsync<T>(Guid organizationId, Func<OrganizationDbContext, Task<T>> read)
    {
        using var ambient = AmbientContext.Begin(organizationId, "onboarding-tests");
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<OrganizationDbContext>());
    }

    [Fact]
    public async Task Creating_an_organization_makes_the_creator_office_admin_with_organization_scope()
    {
        var userId = Guid.CreateVersion7();

        using var response = await CreateAsync(userId, "  Souza & Lima Advogados ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        var organizationId = body.GetProperty("id").GetGuid();
        body.GetProperty("name").GetString().ShouldBe("Souza & Lima Advogados");

        var (member, assignment, admin) = await AsTenantAsync(organizationId, async db =>
        {
            var member = await db.Members.AsNoTracking().SingleAsync(Ct);
            var assignment = await db.RoleAssignments.AsNoTracking().SingleAsync(Ct);
            var admin = await db.Roles.AsNoTracking().Include(r => r.Permissions).SingleAsync(r => r.Id == assignment.RoleId, Ct);
            return (member, assignment, admin);
        });
        member.UserId.ShouldBe(userId);
        assignment.MemberId.ShouldBe(member.Id);
        assignment.Scope.ShouldBe(DataScope.Organization);
        admin.SystemKey.ShouldBe(DefaultRoles.OfficeAdmin);
        admin.Permissions.Count.ShouldBe(PermissionCatalog.Assignable.Count);
    }

    [Fact]
    public async Task Default_roles_are_created_with_their_permissions()
    {
        var organizationId = await CreateOrganizationAsync(Guid.CreateVersion7(), "Escritório Modelo");

        var roles = await AsTenantAsync(organizationId, db => db.Roles.AsNoTracking().Include(r => r.Permissions).ToListAsync(Ct));

        roles.Select(r => r.SystemKey).Order(StringComparer.Ordinal).ShouldBe(DefaultRoles.All.Select(r => r.SystemKey).Order(StringComparer.Ordinal));
        foreach (var expected in DefaultRoles.All)
        {
            roles.Single(r => r.SystemKey == expected.SystemKey).Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal)
                .ShouldBe(expected.Permissions.Order(StringComparer.Ordinal), expected.SystemKey);
        }
    }

    [Fact]
    public async Task Each_organization_only_sees_its_own_members_and_roles()
    {
        var user = Guid.CreateVersion7();
        var first = await CreateOrganizationAsync(user, "Primeiro Escritório");
        var second = await CreateOrganizationAsync(user, "Segundo Escritório");

        var (members, roles) = await AsTenantAsync(first, async db =>
            (await db.Members.AsNoTracking().ToListAsync(Ct), await db.Roles.AsNoTracking().ToListAsync(Ct)));

        members.ShouldAllBe(m => m.OrganizationId == first);
        members.Single().UserId.ShouldBe(user);
        roles.Count.ShouldBe(DefaultRoles.All.Count);
        roles.ShouldAllBe(r => r.OrganizationId == first);
        second.ShouldNotBe(first);
    }

    [Fact]
    public async Task Onboarding_is_audited_under_the_new_organization()
    {
        var organizationId = await CreateOrganizationAsync(Guid.CreateVersion7(), "Escritório Auditado");

        await using var connection = new Npgsql.NpgsqlConnection(dependencies.Settings["ConnectionStrings:lexflow"]);
        await connection.OpenAsync(Ct);
        await using var command = new Npgsql.NpgsqlCommand(
            "SELECT count(*) FROM audit.audit_logs WHERE organization_id = $1 AND entity_type IN ('OrganizationMember', 'Role', 'MemberRoleAssignment')", connection);
        command.Parameters.AddWithValue(organizationId);

        ((long)(await command.ExecuteScalarAsync(Ct))!).ShouldBe(1 + DefaultRoles.All.Count + 1);
    }

    [Fact]
    public async Task Name_is_required_and_anonymous_users_cannot_create()
    {
        using var invalid = await CreateAsync(Guid.CreateVersion7(), " ");
        using var anonymous = await CreateAsync(null, "Escritório");

        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonDocument.Parse(await invalid.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("errors")
            .GetProperty("name")[0].GetProperty("code").GetString().ShouldBe("organization.name_required");
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

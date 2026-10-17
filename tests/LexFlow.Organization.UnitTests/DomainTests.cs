using LexFlow.Organization.Domain;
using LexFlow.Organization.Domain.Members;
using LexFlow.Organization.Domain.OrgStructure;
using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.ValueObjects;
using OrganizationEntity = LexFlow.Organization.Domain.Organizations.Organization;

namespace LexFlow.Organization.UnitTests;

public sealed class DomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Organization_is_created_with_a_trimmed_name_and_optional_mfa()
    {
        var organization = OrganizationEntity.Create(Guid.CreateVersion7(), "  Souza & Lima Advogados ");
        organization.RequireMfa(true);
        organization.Rename(" Souza Lima ");

        organization.Name.ShouldBe("Souza Lima");
        organization.MfaRequired.ShouldBeTrue();
        Should.Throw<ArgumentException>(() => OrganizationEntity.Create(Guid.CreateVersion7(), " "));
    }

    [Fact]
    public void Role_permissions_are_replaced_as_a_set()
    {
        var role = Role.Create(Guid.CreateVersion7(), "Advogado", " Casos da equipe ", "lawyer");

        role.SetPermissions(["cases.read", "cases.edit", " cases.read ", ""]);
        role.SetPermissions(["cases.read", "documents.read"]);

        role.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal).ShouldBe(["cases.read", "documents.read"]);
        role.Permissions.ShouldAllBe(p => p.RoleId == role.Id);
        role.Grants("cases.read").ShouldBeTrue();
        role.Grants("cases.edit").ShouldBeFalse();
        role.Description.ShouldBe("Casos da equipe");
        role.SystemKey.ShouldBe("lawyer");
    }

    [Theory]
    [InlineData(DataScope.Team)]
    [InlineData(DataScope.PracticeArea)]
    [InlineData(DataScope.Branch)]
    public void Team_area_and_branch_scopes_need_a_reference(DataScope scope)
    {
        MemberRoleAssignment.Assign(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), scope).Error
            .ShouldBe(OrganizationErrors.ScopeReferenceRequired);

        var assignment = MemberRoleAssignment.Assign(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), scope, Guid.CreateVersion7());
        assignment.IsSuccess.ShouldBeTrue();
        assignment.Value.Scope.ShouldBe(scope);
    }

    [Theory]
    [InlineData(DataScope.Own)]
    [InlineData(DataScope.Organization)]
    public void Own_and_organization_scopes_take_no_reference(DataScope scope)
    {
        MemberRoleAssignment.Assign(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), scope, Guid.CreateVersion7()).Error
            .ShouldBe(OrganizationErrors.ScopeReferenceNotAllowed);
        MemberRoleAssignment.Assign(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), scope).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.0001)]
    public void Partner_share_must_be_above_zero_and_at_most_100(decimal share) =>
        Partner.Create(Guid.CreateVersion7(), "Ana Souza", share).Error.ShouldBe(OrganizationErrors.InvalidPartnerShare);

    [Fact]
    public void Structure_entities_keep_their_data()
    {
        var memberId = Guid.CreateVersion7();
        var partner = Partner.Create(Guid.CreateVersion7(), " Ana Souza ", 33.3333m, memberId).Value;
        var center = CostCenter.Create(Guid.CreateVersion7(), " adm-01 ", "Administrativo");
        var branch = Branch.Create(Guid.CreateVersion7(), "São Paulo", Cnpj.Create("11.222.333/0001-81").Value, " 1234 ");
        var area = PracticeArea.Create(Guid.CreateVersion7(), "Trabalhista");
        var team = Team.Create(Guid.CreateVersion7(), "Equipe A", area.Id, branch.Id);
        var teamMember = TeamMember.Add(Guid.CreateVersion7(), team.Id, memberId);
        team.Rename(" Equipe B ");

        partner.Name.ShouldBe("Ana Souza");
        partner.SharePercentage.ShouldBe(33.3333m);
        partner.MemberId.ShouldBe(memberId);
        center.Code.ShouldBe("ADM-01");
        branch.Cnpj.ShouldBe("11222333000181");
        branch.MunicipalRegistration.ShouldBe("1234");
        team.Name.ShouldBe("Equipe B");
        team.PracticeAreaId.ShouldBe(area.Id);
        team.BranchId.ShouldBe(branch.Id);
        teamMember.TeamId.ShouldBe(team.Id);
        Should.Throw<ArgumentException>(() => PracticeArea.Create(Guid.CreateVersion7(), ""));
    }

    [Fact]
    public void A_member_needs_a_user()
    {
        var organization = Guid.CreateVersion7();
        var user = Guid.CreateVersion7();

        var member = OrganizationMember.Join(Guid.CreateVersion7(), organization, user, Now);

        member.OrganizationId.ShouldBe(organization);
        member.UserId.ShouldBe(user);
        member.JoinedAt.ShouldBe(Now);
        Should.Throw<ArgumentException>(() => OrganizationMember.Join(Guid.CreateVersion7(), organization, Guid.Empty, Now));
    }
}

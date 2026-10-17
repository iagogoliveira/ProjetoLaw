using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Organization.Domain.Roles;

namespace LexFlow.Organization.UnitTests;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Every_permission_is_unique_well_formed_and_described()
    {
        var names = PermissionCatalog.All.Select(p => p.Name).ToList();

        names.Distinct(StringComparer.Ordinal).Count().ShouldBe(names.Count);
        PermissionCatalog.All.ShouldAllBe(p => PermissionCatalog.IsWellFormed(p.Name) && p.Description.Length > 0 && p.Group.Length > 0);
    }

    [Theory]
    [InlineData("cases.read", true)]
    [InlineData("settings.users.manage", true)]
    [InlineData("finance.fee_agreements.read", true)]
    [InlineData("Cases.Read", false)]
    [InlineData("cases", false)]
    [InlineData("a.b.c.d", false)]
    [InlineData("cases..read", false)]
    public void Names_follow_module_resource_action(string name, bool wellFormed) =>
        PermissionCatalog.IsWellFormed(name).ShouldBe(wellFormed);

    [Fact]
    public void Account_permissions_are_universal_and_stay_out_of_the_role_matrix()
    {
        PermissionCatalog.IsUniversal(PermissionCatalog.ProfileRead).ShouldBeTrue();
        PermissionCatalog.IsUniversal(PermissionCatalog.ProfileManage).ShouldBeTrue();
        PermissionCatalog.IsUniversal(PermissionCatalog.OrganizationsCreate).ShouldBeTrue();
        PermissionCatalog.IsUniversal("cases.read").ShouldBeFalse();
        PermissionCatalog.Assignable.ShouldNotContain(p => p.Universal);
        PermissionCatalog.Exists("cases.read").ShouldBeTrue();
        PermissionCatalog.Exists("cases.fly").ShouldBeFalse();
    }

    [Fact]
    public void Spec_examples_are_in_the_catalog()
    {
        foreach (var name in new[] { "cases.read", "cases.edit", "finance.invoices.approve", "ai.drafts.generate", "settings.users.manage", "platform.jobs.read" })
        {
            PermissionCatalog.Exists(name).ShouldBeTrue(name);
        }
    }

    [Fact]
    public void Default_roles_are_the_six_internal_profiles_of_section_3_2()
    {
        DefaultRoles.All.Select(r => r.SystemKey).ShouldBe(
            [DefaultRoles.OfficeAdmin, DefaultRoles.Partner, DefaultRoles.Lawyer, DefaultRoles.Paralegal, DefaultRoles.Controller, DefaultRoles.Finance]);
        DefaultRoles.All.Select(r => r.Name).Distinct(StringComparer.Ordinal).Count().ShouldBe(6);
    }

    [Fact]
    public void Default_roles_only_grant_assignable_catalog_permissions_once()
    {
        foreach (var role in DefaultRoles.All)
        {
            role.Permissions.ShouldAllBe(p => PermissionCatalog.Exists(p) && !PermissionCatalog.IsUniversal(p), role.SystemKey);
            role.Permissions.Distinct(StringComparer.Ordinal).Count().ShouldBe(role.Permissions.Count, role.SystemKey);
        }
    }

    [Fact]
    public void Office_admin_has_everything_and_partner_everything_but_settings_changes()
    {
        var admin = DefaultRoles.All.Single(r => r.SystemKey == DefaultRoles.OfficeAdmin);
        var partner = DefaultRoles.All.Single(r => r.SystemKey == DefaultRoles.Partner);

        admin.Permissions.Count.ShouldBe(PermissionCatalog.Assignable.Count);
        admin.SuggestedScope.ShouldBe(DataScope.Organization);
        partner.Permissions.ShouldContain("finance.invoices.approve");
        partner.Permissions.ShouldContain(PermissionCatalog.SettingsUsersRead);
        partner.Permissions.ShouldNotContain(PermissionCatalog.SettingsUsersManage);
        partner.Permissions.ShouldNotContain(PermissionCatalog.SettingsRolesManage);
    }

    [Fact]
    public void Paralegal_cannot_approve_or_delete_and_finance_sees_no_legal_content()
    {
        var paralegal = DefaultRoles.All.Single(r => r.SystemKey == DefaultRoles.Paralegal);
        var finance = DefaultRoles.All.Single(r => r.SystemKey == DefaultRoles.Finance);
        var lawyer = DefaultRoles.All.Single(r => r.SystemKey == DefaultRoles.Lawyer);

        paralegal.Permissions.ShouldNotContain(p => p.EndsWith(".approve", StringComparison.Ordinal) || p.EndsWith(".delete", StringComparison.Ordinal));
        paralegal.SuggestedScope.ShouldBe(DataScope.Own);
        finance.Permissions.ShouldNotContain(p => p.StartsWith("cases.", StringComparison.Ordinal) || p.StartsWith("documents.", StringComparison.Ordinal) || p.StartsWith("publications.", StringComparison.Ordinal));
        finance.Permissions.ShouldContain("finance.invoices.approve");
        lawyer.SuggestedScope.ShouldBe(DataScope.Team);
        // Exceção decidida em 2026-10-17 (ADR 0021): todo perfil lê os campos customizados para preencher formulários.
        lawyer.Permissions.Where(p => p.StartsWith("settings.", StringComparison.Ordinal)).ShouldBe([PermissionCatalog.SettingsCustomFieldsRead]);
        DefaultRoles.All.ShouldAllBe(r => r.Permissions.Contains(PermissionCatalog.SettingsCustomFieldsRead));
    }

    [Fact]
    public async Task Organization_name_is_required_and_limited()
    {
        var validator = new CreateOrganizationValidator();
        var ct = TestContext.Current.CancellationToken;

        (await validator.ValidateAsync(new CreateOrganizationCommand(""), ct)).Errors.Single().ErrorCode.ShouldBe("organization.name_required");
        (await validator.ValidateAsync(new CreateOrganizationCommand(new string('a', 201)), ct)).Errors.Single().ErrorCode.ShouldBe("organization.name_too_long");
        (await validator.ValidateAsync(new CreateOrganizationCommand("Souza Lima"), ct)).IsValid.ShouldBeTrue();
    }
}

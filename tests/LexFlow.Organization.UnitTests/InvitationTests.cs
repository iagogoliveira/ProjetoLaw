using LexFlow.Organization.Application.Invitations;
using LexFlow.Organization.Domain;
using LexFlow.Organization.Domain.Invitations;
using LexFlow.Organization.Domain.OrgStructure;
using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Organization.UnitTests;

public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static MemberRoleAssignment Lawyer() => MemberRoleAssignment.Assign(Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7(), DataScope.Own).Value;

    private static Invitation Pending() => Invitation.Create(Guid.CreateVersion7(), " Ana@Exemplo.com.br ", Guid.CreateVersion7(), [Lawyer()], Now).Value;

    [Fact]
    public void Invitation_normalizes_the_email_keeps_the_roles_and_lasts_7_days()
    {
        var role = Lawyer();

        var invitation = Invitation.Create(Guid.CreateVersion7(), " Ana@Exemplo.com.br ", Guid.CreateVersion7(), [role], Now).Value;

        invitation.Email.ShouldBe("ana@exemplo.com.br");
        invitation.Status.ShouldBe(InvitationStatus.Pending);
        invitation.ExpiresAt.ShouldBe(Now.AddDays(7));
        invitation.Roles.Single().RoleId.ShouldBe(role.RoleId);
        invitation.Roles.Single().InvitationId.ShouldBe(invitation.Id);
        invitation.IsOpen(Now.AddDays(7).AddTicks(-1)).ShouldBeTrue();
        invitation.IsExpired(Now.AddDays(7)).ShouldBeTrue();
    }

    [Fact]
    public void Invitation_needs_a_valid_email_and_at_least_one_role()
    {
        Invitation.Create(Guid.CreateVersion7(), "sem-arroba", Guid.CreateVersion7(), [Lawyer()], Now).Error.ShouldBe(OrganizationErrors.InvalidEmail);
        Invitation.Create(Guid.CreateVersion7(), "ana@exemplo.com.br", Guid.CreateVersion7(), [], Now).Error.ShouldBe(OrganizationErrors.InvitationRolesRequired);
    }

    [Fact]
    public void Accepting_is_single_use_and_refused_after_expiry()
    {
        var invitation = Pending();
        var expired = Pending();
        var member = Guid.CreateVersion7();

        invitation.Accept(member, Now.AddDays(1)).IsSuccess.ShouldBeTrue();
        invitation.Accept(member, Now.AddDays(1)).Error.ShouldBe(OrganizationErrors.InvitationNotFound);
        expired.Accept(member, Now.AddDays(8)).Error.ShouldBe(OrganizationErrors.InvitationExpired);

        invitation.Status.ShouldBe(InvitationStatus.Accepted);
        invitation.MemberId.ShouldBe(member);
        invitation.AcceptedAt.ShouldBe(Now.AddDays(1));
        invitation.IsExpired(Now.AddDays(30)).ShouldBeFalse("só convite pendente expira");
    }

    [Fact]
    public void Renewing_restarts_the_7_days_and_revoking_ends_the_invitation()
    {
        var invitation = Pending();

        invitation.Renew(Now.AddDays(10)).IsSuccess.ShouldBeTrue();
        invitation.ExpiresAt.ShouldBe(Now.AddDays(17));
        invitation.Revoke(Now.AddDays(11)).IsSuccess.ShouldBeTrue();

        invitation.Status.ShouldBe(InvitationStatus.Revoked);
        invitation.RevokedAt.ShouldBe(Now.AddDays(11));
        invitation.Renew(Now.AddDays(12)).Error.ShouldBe(OrganizationErrors.InvitationNotPending);
        invitation.Revoke(Now.AddDays(12)).Error.ShouldBe(OrganizationErrors.InvitationNotPending);
    }

    [Fact]
    public void Tokens_are_random_url_safe_and_only_their_hash_is_kept()
    {
        var first = InvitationTokens.NewToken();
        var second = InvitationTokens.NewToken();

        first.ShouldNotBe(second);
        first.ShouldMatch("^[A-Za-z0-9_-]{43}$");
        InvitationTokens.Hash(first).ShouldBe(InvitationTokens.Hash(first));
        InvitationTokens.Hash(first).Length.ShouldBe(InvitationToken.HashLength);
        InvitationTokens.Hash(first).ShouldNotContain(first);
    }

    [Fact]
    public void Structure_items_are_updated_in_place()
    {
        var branch = Branch.Create(Guid.CreateVersion7(), "Matriz", Cnpj.Create("11222333000181").Value, "123");
        var team = Team.Create(Guid.CreateVersion7(), "Equipe");
        var costCenter = CostCenter.Create(Guid.CreateVersion7(), "adm", "Administrativo");
        var partner = Partner.Create(Guid.CreateVersion7(), "Ana", 50).Value;
        var role = Role.Create(Guid.CreateVersion7(), "Advogado", "Casos");
        var area = Guid.CreateVersion7();

        branch.Update(" Matriz SP ", null, " ");
        team.Update("Equipe A", area, null);
        costCenter.Update(" fin-01 ", "Financeiro");
        partner.Update("Ana Souza", 60, null).IsSuccess.ShouldBeTrue();
        role.Rename(" Advogada ", " ");

        branch.Name.ShouldBe("Matriz SP");
        branch.Cnpj.ShouldBeNull();
        branch.MunicipalRegistration.ShouldBeNull();
        team.PracticeAreaId.ShouldBe(area);
        costCenter.Code.ShouldBe("FIN-01");
        partner.SharePercentage.ShouldBe(60);
        partner.Update("Ana Souza", 101, null).Error.ShouldBe(OrganizationErrors.InvalidPartnerShare);
        role.Name.ShouldBe("Advogada");
        role.Description.ShouldBeNull();
    }
}

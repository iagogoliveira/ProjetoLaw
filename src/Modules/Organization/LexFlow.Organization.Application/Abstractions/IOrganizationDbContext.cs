using LexFlow.Organization.Domain.Invitations;
using LexFlow.Organization.Domain.Members;
using LexFlow.Organization.Domain.OrgStructure;
using LexFlow.Organization.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using OrganizationEntity = LexFlow.Organization.Domain.Organizations.Organization;

namespace LexFlow.Organization.Application.Abstractions;

/// <summary>Acesso aos dados do módulo pelos handlers (implementado pelo <c>OrganizationDbContext</c>).</summary>
public interface IOrganizationDbContext
{
    DbSet<OrganizationEntity> Organizations { get; }

    DbSet<OrganizationMember> Members { get; }

    /// <summary>Diretório usuário → organizações, sem tenant (ver <see cref="UserOrganization"/>).</summary>
    DbSet<UserOrganization> UserOrganizations { get; }

    DbSet<Role> Roles { get; }

    DbSet<MemberRoleAssignment> RoleAssignments { get; }

    DbSet<Branch> Branches { get; }

    DbSet<PracticeArea> PracticeAreas { get; }

    DbSet<Team> Teams { get; }

    DbSet<TeamMember> TeamMembers { get; }

    DbSet<CostCenter> CostCenters { get; }

    DbSet<Partner> Partners { get; }

    DbSet<Invitation> Invitations { get; }

    /// <summary>Diretório token → convite, sem tenant (ver <see cref="InvitationToken"/>).</summary>
    DbSet<InvitationToken> InvitationTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

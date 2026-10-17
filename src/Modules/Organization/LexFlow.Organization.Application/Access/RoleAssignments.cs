using LexFlow.Application.Abstractions;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Domain;
using LexFlow.Organization.Domain.Invitations;
using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Organization.Application.Access;

public sealed record RoleAssignmentInput(Guid RoleId, DataScope Scope, Guid? ScopeRefId);

/// <summary>
/// Regras comuns às atribuições de perfil (tela de membros e convites): os perfis são da organização ativa e a equipe,
/// a área ou a filial do escopo também. Diz ainda se um perfil ou um item da estrutura está em uso.
/// </summary>
internal sealed class RoleAssignments(IOrganizationDbContext db, IIdGenerator ids)
{
    public async Task<Result<IReadOnlyList<MemberRoleAssignment>>> BuildAsync(Guid memberId, IReadOnlyList<RoleAssignmentInput> inputs, CancellationToken cancellationToken)
    {
        var roleIds = inputs.Select(r => r.RoleId).Distinct().ToList();
        if (await db.Roles.CountAsync(r => roleIds.Contains(r.Id), cancellationToken) != roleIds.Count)
        {
            return OrganizationErrors.RoleNotFound;
        }

        var assignments = new List<MemberRoleAssignment>();
        foreach (var input in inputs)
        {
            if (input.ScopeRefId is { } reference && !await ScopeExistsAsync(input.Scope, reference, cancellationToken))
            {
                return OrganizationErrors.ScopeReferenceNotFound;
            }

            var assignment = MemberRoleAssignment.Assign(ids.NewId(), memberId, input.RoleId, input.Scope, input.ScopeRefId);
            if (assignment.IsFailure)
            {
                return assignment.Error;
            }

            assignments.Add(assignment.Value);
        }

        return assignments;
    }

    public Task<Guid> AdminRoleIdAsync(CancellationToken cancellationToken) =>
        db.Roles.Where(r => r.SystemKey == DefaultRoles.OfficeAdmin).Select(r => r.Id).SingleAsync(cancellationToken);

    /// <summary>Se, sem as atribuições de <paramref name="memberId"/>, sobra outro Administrador do escritório.</summary>
    public async Task<bool> AnotherAdminExistsAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var adminRoleId = await AdminRoleIdAsync(cancellationToken);
        return await db.RoleAssignments.AnyAsync(a => a.RoleId == adminRoleId && a.MemberId != memberId, cancellationToken);
    }

    /// <summary>Perfil atribuído a algum membro ou previsto num convite pendente.</summary>
    public async Task<bool> RoleInUseAsync(Guid roleId, CancellationToken cancellationToken) =>
        await db.RoleAssignments.AnyAsync(a => a.RoleId == roleId, cancellationToken)
        || await db.Invitations.AnyAsync(i => i.Status == InvitationStatus.Pending && i.Roles.Any(r => r.RoleId == roleId), cancellationToken);

    /// <summary>Equipe, área ou filial usada como escopo de uma atribuição ou de um convite pendente.</summary>
    public async Task<bool> ScopeInUseAsync(DataScope scope, Guid reference, CancellationToken cancellationToken) =>
        await db.RoleAssignments.AnyAsync(a => a.Scope == scope && a.ScopeRefId == reference, cancellationToken)
        || await db.Invitations.AnyAsync(i => i.Status == InvitationStatus.Pending && i.Roles.Any(r => r.Scope == scope && r.ScopeRefId == reference), cancellationToken);

    private Task<bool> ScopeExistsAsync(DataScope scope, Guid reference, CancellationToken cancellationToken) => scope switch
    {
        DataScope.Team => db.Teams.AnyAsync(t => t.Id == reference, cancellationToken),
        DataScope.PracticeArea => db.PracticeAreas.AnyAsync(a => a.Id == reference, cancellationToken),
        DataScope.Branch => db.Branches.AnyAsync(b => b.Id == reference, cancellationToken),
        _ => Task.FromResult(false),
    };
}

using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Contracts;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Organization.Domain;
using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Organization.Application.Access;

public sealed record OrganizationSummary(Guid Id, string Name);

/// <summary>
/// Acesso do usuário: organizações das quais é membro, a ativa (a da requisição) e as permissões nela, para o SPA
/// esconder o que não cabe (a UI só esconde; quem bloqueia é o servidor).
/// </summary>
public sealed record MyAccessResponse(
    Guid? ActiveOrganizationId,
    IReadOnlyList<OrganizationSummary> Organizations,
    IReadOnlyList<string> Permissions,
    bool MfaRequired);

public sealed record GetMyAccessQuery(Guid UserId) : IQuery<MyAccessResponse>;

internal sealed class GetMyAccessHandler(IOrganizationDbContext db, ITenantContext tenant, AccessReader access, LexFlow.Application.Abstractions.Security.IMembershipLookup memberships)
    : IQueryHandler<GetMyAccessQuery, MyAccessResponse>
{
    private static readonly string[] Universal = [.. PermissionCatalog.All.Where(p => p.Universal).Select(p => p.Name)];

    public async Task<Result<MyAccessResponse>> Handle(GetMyAccessQuery query, CancellationToken cancellationToken)
    {
        var organizationIds = (await memberships.GetAsync(query.UserId, cancellationToken)).OrganizationIds;
        var organizations = await db.Organizations.AsNoTracking()
            .Where(o => organizationIds.Contains(o.Id))
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationSummary(o.Id, o.Name))
            .ToListAsync(cancellationToken);

        var active = tenant.OrganizationId;
        var snapshot = active is { } organizationId ? await access.GetAsync(organizationId, query.UserId, cancellationToken) : null;
        var permissions = Universal.Concat(snapshot?.Permissions.Keys ?? []).Order(StringComparer.Ordinal).ToList();
        return new MyAccessResponse(active, organizations, permissions, snapshot?.MfaRequired ?? false);
    }
}

/// <summary>Troca de organização (RF-02): grava a escolha, usada quando a requisição não traz <c>X-Organization-Id</c>.</summary>
public sealed record SelectActiveOrganizationCommand(Guid UserId, Guid OrganizationId) : ICommand;

internal sealed class SelectActiveOrganizationHandler(IOrganizationDbContext db, IAccessCache cache, IClock clock)
    : ICommandHandler<SelectActiveOrganizationCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SelectActiveOrganizationCommand command, CancellationToken cancellationToken)
    {
        var membership = await db.UserOrganizations
            .SingleOrDefaultAsync(u => u.UserId == command.UserId && u.OrganizationId == command.OrganizationId, cancellationToken);
        if (membership is null)
        {
            return OrganizationErrors.NotAMember;
        }

        membership.Select(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateMembershipsAsync(command.UserId, cancellationToken);
        return Unit.Value;
    }
}

public sealed record RoleResponse(Guid Id, string Name, string? Description, string? SystemKey, IReadOnlyList<string> Permissions);

public sealed record ListRolesQuery : IQuery<IReadOnlyList<RoleResponse>>;

internal sealed class ListRolesHandler(IOrganizationDbContext db) : IQueryHandler<ListRolesQuery, IReadOnlyList<RoleResponse>>
{
    public async Task<Result<IReadOnlyList<RoleResponse>>> Handle(ListRolesQuery query, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking().Include(r => r.Permissions).OrderBy(r => r.Name).ToListAsync(cancellationToken);
        return roles.Select(r => new RoleResponse(r.Id, r.Name, r.Description, r.SystemKey, [.. r.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal)])).ToList();
    }
}

public sealed record AssignmentResponse(Guid RoleId, DataScope Scope, Guid? ScopeRefId);

/// <summary>Membro com nome e e-mail da conta (módulo Identity) e os perfis atribuídos.</summary>
public sealed record MemberResponse(Guid Id, Guid UserId, string? FullName, string? Email, DateTimeOffset JoinedAt, IReadOnlyList<AssignmentResponse> Roles);

public sealed record ListMembersQuery : IQuery<IReadOnlyList<MemberResponse>>;

internal sealed class ListMembersHandler(IOrganizationDbContext db, IUserDirectory users) : IQueryHandler<ListMembersQuery, IReadOnlyList<MemberResponse>>
{
    public async Task<Result<IReadOnlyList<MemberResponse>>> Handle(ListMembersQuery query, CancellationToken cancellationToken)
    {
        var members = await db.Members.AsNoTracking().OrderBy(m => m.JoinedAt).ToListAsync(cancellationToken);
        var assignments = (await db.RoleAssignments.AsNoTracking().ToListAsync(cancellationToken)).ToLookup(a => a.MemberId);
        var accounts = (await users.GetManyAsync([.. members.Select(m => m.UserId)], cancellationToken)).ToDictionary(u => u.Id);
        return members.Select(m => new MemberResponse(
            m.Id,
            m.UserId,
            accounts.GetValueOrDefault(m.UserId)?.FullName,
            accounts.GetValueOrDefault(m.UserId)?.Email,
            m.JoinedAt,
            [.. assignments[m.Id].Select(a => new AssignmentResponse(a.RoleId, a.Scope, a.ScopeRefId))])).ToList();
    }
}

/// <summary>
/// Remove um membro (exclusão lógica): perde o acesso na próxima requisição, sai das equipes e do diretório de
/// organizações. O escritório nunca fica sem Administrador do escritório. O histórico (autoria, auditoria) continua.
/// </summary>
public sealed record RemoveMemberCommand(Guid MemberId) : ICommand;

internal sealed class RemoveMemberHandler(IOrganizationDbContext db, AccessChanges changes, RoleAssignments rules, IAccessCache cache)
    : ICommandHandler<RemoveMemberCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var member = await db.Members.SingleOrDefaultAsync(m => m.Id == command.MemberId, cancellationToken);
        if (member is null)
        {
            return OrganizationErrors.MemberNotFound;
        }

        if (!await rules.AnotherAdminExistsAsync(member.Id, cancellationToken))
        {
            return OrganizationErrors.LastAdmin;
        }

        db.RoleAssignments.RemoveRange(await db.RoleAssignments.Where(a => a.MemberId == member.Id).ToListAsync(cancellationToken));
        db.TeamMembers.RemoveRange(await db.TeamMembers.Where(t => t.MemberId == member.Id).ToListAsync(cancellationToken));
        db.UserOrganizations.RemoveRange(await db.UserOrganizations.Where(u => u.UserId == member.UserId && u.OrganizationId == member.OrganizationId).ToListAsync(cancellationToken));
        db.Members.Remove(member);
        await changes.SaveAsync(null, cancellationToken);
        await cache.InvalidateMembershipsAsync(member.UserId, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Substitui as permissões de um perfil (tela de perfis). O Administrador do escritório fica sempre com todas.</summary>
public sealed record UpdateRolePermissionsCommand(Guid RoleId, IReadOnlyList<string> Permissions) : ICommand;

internal sealed class UpdateRolePermissionsValidator : AbstractValidator<UpdateRolePermissionsCommand>
{
    public UpdateRolePermissionsValidator()
    {
        RuleFor(c => c.Permissions).NotNull();
        RuleForEach(c => c.Permissions)
            .Must(p => PermissionCatalog.Exists(p) && !PermissionCatalog.IsUniversal(p))
            .WithErrorCode(OrganizationErrors.UnknownPermission.Code);
    }
}

internal sealed class UpdateRolePermissionsHandler(IOrganizationDbContext db, AccessChanges changes)
    : ICommandHandler<UpdateRolePermissionsCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        var role = await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);
        if (role is null)
        {
            return OrganizationErrors.RoleNotFound;
        }

        if (string.Equals(role.SystemKey, DefaultRoles.OfficeAdmin, StringComparison.Ordinal))
        {
            return OrganizationErrors.AdminRoleLocked;
        }

        role.SetPermissions(command.Permissions);
        await changes.SaveAsync(role.Id, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// Substitui os perfis de um membro, cada um com escopo de dados. A equipe, a área ou a filial do escopo precisam ser
/// da organização, e o escritório nunca fica sem Administrador do escritório.
/// </summary>
public sealed record AssignMemberRolesCommand(Guid MemberId, IReadOnlyList<RoleAssignmentInput> Roles) : ICommand;

internal sealed class AssignMemberRolesHandler(IOrganizationDbContext db, AccessChanges changes, RoleAssignments rules)
    : ICommandHandler<AssignMemberRolesCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AssignMemberRolesCommand command, CancellationToken cancellationToken)
    {
        if (!await db.Members.AnyAsync(m => m.Id == command.MemberId, cancellationToken))
        {
            return OrganizationErrors.MemberNotFound;
        }

        var assignments = await rules.BuildAsync(command.MemberId, command.Roles, cancellationToken);
        if (assignments.IsFailure)
        {
            return assignments.Error;
        }

        var adminRoleId = await rules.AdminRoleIdAsync(cancellationToken);
        if (!assignments.Value.Any(a => a.RoleId == adminRoleId) && !await rules.AnotherAdminExistsAsync(command.MemberId, cancellationToken))
        {
            return OrganizationErrors.LastAdmin;
        }

        db.RoleAssignments.RemoveRange(await db.RoleAssignments.Where(a => a.MemberId == command.MemberId).ToListAsync(cancellationToken));
        db.RoleAssignments.AddRange(assignments.Value);
        await changes.SaveAsync(null, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Liga ou desliga o MFA obrigatório para todos os membros da organização ativa (RF-01).</summary>
public sealed record SetMfaPolicyCommand(bool Required) : ICommand;

internal sealed class SetMfaPolicyHandler(IOrganizationDbContext db, ITenantContext tenant, AccessChanges changes)
    : ICommandHandler<SetMfaPolicyCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SetMfaPolicyCommand command, CancellationToken cancellationToken)
    {
        var organization = await db.Organizations.SingleAsync(o => o.Id == tenant.RequiredOrganizationId, cancellationToken);
        organization.RequireMfa(command.Required);
        await changes.SaveAsync(null, cancellationToken);
        return Unit.Value;
    }
}

/// <summary>
/// Grava uma mudança de acesso e invalida o cache da organização: logo após gravar (efeito imediato nesta instância e
/// no Redis compartilhado) e de novo pelo evento, consumido depois do commit (cobre leituras concorrentes que
/// tenham guardado o estado anterior no intervalo).
/// </summary>
internal sealed class AccessChanges(IOrganizationDbContext db, IIntegrationEventPublisher events, IAccessCache cache, ITenantContext tenant)
{
    public async Task SaveAsync(Guid? roleId, CancellationToken cancellationToken)
    {
        events.Publish(new RolePermissionsChangedIntegrationEvent(roleId));
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateOrganizationAsync(tenant.RequiredOrganizationId, cancellationToken);
    }
}

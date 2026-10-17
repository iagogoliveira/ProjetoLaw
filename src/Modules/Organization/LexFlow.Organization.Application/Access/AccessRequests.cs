using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
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

public sealed record MemberResponse(Guid Id, Guid UserId, DateTimeOffset JoinedAt, IReadOnlyList<AssignmentResponse> Roles);

public sealed record ListMembersQuery : IQuery<IReadOnlyList<MemberResponse>>;

internal sealed class ListMembersHandler(IOrganizationDbContext db) : IQueryHandler<ListMembersQuery, IReadOnlyList<MemberResponse>>
{
    public async Task<Result<IReadOnlyList<MemberResponse>>> Handle(ListMembersQuery query, CancellationToken cancellationToken)
    {
        var members = await db.Members.AsNoTracking().OrderBy(m => m.JoinedAt).ToListAsync(cancellationToken);
        var assignments = (await db.RoleAssignments.AsNoTracking().ToListAsync(cancellationToken)).ToLookup(a => a.MemberId);
        return members.Select(m => new MemberResponse(
            m.Id,
            m.UserId,
            m.JoinedAt,
            [.. assignments[m.Id].Select(a => new AssignmentResponse(a.RoleId, a.Scope, a.ScopeRefId))])).ToList();
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

public sealed record RoleAssignmentInput(Guid RoleId, DataScope Scope, Guid? ScopeRefId);

/// <summary>
/// Substitui os perfis de um membro, cada um com escopo de dados. A equipe, a área ou a filial do escopo precisam ser
/// da organização, e o escritório nunca fica sem Administrador do escritório.
/// </summary>
public sealed record AssignMemberRolesCommand(Guid MemberId, IReadOnlyList<RoleAssignmentInput> Roles) : ICommand;

internal sealed class AssignMemberRolesHandler(IOrganizationDbContext db, AccessChanges changes, IIdGenerator ids)
    : ICommandHandler<AssignMemberRolesCommand, Unit>
{
    public async Task<Result<Unit>> Handle(AssignMemberRolesCommand command, CancellationToken cancellationToken)
    {
        if (!await db.Members.AnyAsync(m => m.Id == command.MemberId, cancellationToken))
        {
            return OrganizationErrors.MemberNotFound;
        }

        var roleIds = command.Roles.Select(r => r.RoleId).Distinct().ToList();
        var roles = await db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.Id)).Select(r => new { r.Id, r.SystemKey }).ToListAsync(cancellationToken);
        if (roles.Count != roleIds.Count)
        {
            return OrganizationErrors.RoleNotFound;
        }

        var assignments = new List<MemberRoleAssignment>();
        foreach (var input in command.Roles)
        {
            if (input.ScopeRefId is { } reference && !await ScopeExistsAsync(input.Scope, reference, cancellationToken))
            {
                return OrganizationErrors.ScopeReferenceNotFound;
            }

            var assignment = MemberRoleAssignment.Assign(ids.NewId(), command.MemberId, input.RoleId, input.Scope, input.ScopeRefId);
            if (assignment.IsFailure)
            {
                return assignment.Error;
            }

            assignments.Add(assignment.Value);
        }

        var adminRoleId = await db.Roles.Where(r => r.SystemKey == DefaultRoles.OfficeAdmin).Select(r => r.Id).SingleAsync(cancellationToken);
        var keepsAdmin = assignments.Exists(a => a.RoleId == adminRoleId)
            || await db.RoleAssignments.AnyAsync(a => a.RoleId == adminRoleId && a.MemberId != command.MemberId, cancellationToken);
        if (!keepsAdmin)
        {
            return OrganizationErrors.LastAdmin;
        }

        db.RoleAssignments.RemoveRange(await db.RoleAssignments.Where(a => a.MemberId == command.MemberId).ToListAsync(cancellationToken));
        db.RoleAssignments.AddRange(assignments);
        await changes.SaveAsync(null, cancellationToken);
        return Unit.Value;
    }

    private Task<bool> ScopeExistsAsync(DataScope scope, Guid reference, CancellationToken cancellationToken) => scope switch
    {
        DataScope.Team => db.Teams.AnyAsync(t => t.Id == reference, cancellationToken),
        DataScope.PracticeArea => db.PracticeAreas.AnyAsync(a => a.Id == reference, cancellationToken),
        DataScope.Branch => db.Branches.AnyAsync(b => b.Id == reference, cancellationToken),
        _ => Task.FromResult(false),
    };
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

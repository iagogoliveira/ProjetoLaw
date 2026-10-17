using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Organization.Domain;
using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Organization.Application.Access;

public sealed record PermissionResponse(string Name, string Description);

public sealed record PermissionGroupResponse(string Group, IReadOnlyList<PermissionResponse> Permissions);

/// <summary>Catálogo para a matriz da tela de perfis: só as permissões atribuíveis, na ordem e nos grupos do catálogo.</summary>
public sealed record ListPermissionsQuery : IQuery<IReadOnlyList<PermissionGroupResponse>>;

internal sealed class ListPermissionsHandler : IQueryHandler<ListPermissionsQuery, IReadOnlyList<PermissionGroupResponse>>
{
    private static readonly IReadOnlyList<PermissionGroupResponse> Groups = [.. PermissionCatalog.Assignable
        .GroupBy(p => p.Group, StringComparer.Ordinal)
        .Select(g => new PermissionGroupResponse(g.Key, [.. g.Select(p => new PermissionResponse(p.Name, p.Description))]))];

    public Task<Result<IReadOnlyList<PermissionGroupResponse>>> Handle(ListPermissionsQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<IReadOnlyList<PermissionGroupResponse>>>(Result.Ok(Groups));
}

/// <summary>Perfil criado pelo escritório, com nome único e a matriz de permissões inicial.</summary>
public sealed record CreateRoleCommand(string Name, string? Description, IReadOnlyList<string> Permissions) : ICommand<RoleResponse>;

/// <summary>Renomeia um perfil (inclusive os padrão) e troca a descrição.</summary>
public sealed record UpdateRoleCommand(Guid RoleId, string Name, string? Description) : ICommand<RoleResponse>;

/// <summary>Exclui um perfil sem membros nem convites pendentes. O Administrador do escritório não pode ser excluído.</summary>
public sealed record DeleteRoleCommand(Guid RoleId) : ICommand;

internal static class RoleRules
{
    public static IRuleBuilderOptions<T, string> RoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode("organization.role_name_required")
            .MaximumLength(Role.MaxNameLength).WithErrorCode("organization.role_name_too_long");

    public static IRuleBuilderOptions<T, string?> RoleDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(Role.MaxDescriptionLength).WithErrorCode("organization.role_description_too_long");

    public static RoleResponse ToResponse(this Role role) =>
        new(role.Id, role.Name, role.Description, role.SystemKey, [.. role.Permissions.Select(p => p.Permission).Order(StringComparer.Ordinal)]);
}

internal sealed class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(c => c.Name).RoleName();
        RuleFor(c => c.Description).RoleDescription();
        RuleFor(c => c.Permissions).NotNull();
        RuleForEach(c => c.Permissions)
            .Must(p => PermissionCatalog.Exists(p) && !PermissionCatalog.IsUniversal(p))
            .WithErrorCode(OrganizationErrors.UnknownPermission.Code);
    }
}

internal sealed class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(c => c.Name).RoleName();
        RuleFor(c => c.Description).RoleDescription();
    }
}

internal sealed class CreateRoleHandler(IOrganizationDbContext db, IIdGenerator ids) : ICommandHandler<CreateRoleCommand, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        var name = command.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name, cancellationToken))
        {
            return OrganizationErrors.NameTaken;
        }

        var role = Role.Create(ids.NewId(), name, command.Description);
        role.SetPermissions(command.Permissions);
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return role.ToResponse();
    }
}

internal sealed class UpdateRoleHandler(IOrganizationDbContext db) : ICommandHandler<UpdateRoleCommand, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await db.Roles.Include(r => r.Permissions).SingleOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);
        if (role is null)
        {
            return OrganizationErrors.RoleNotFound;
        }

        var name = command.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name && r.Id != role.Id, cancellationToken))
        {
            return OrganizationErrors.NameTaken;
        }

        role.Rename(name, command.Description);
        await db.SaveChangesAsync(cancellationToken);
        return role.ToResponse();
    }
}

internal sealed class DeleteRoleHandler(IOrganizationDbContext db, RoleAssignments rules) : ICommandHandler<DeleteRoleCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);
        if (role is null)
        {
            return OrganizationErrors.RoleNotFound;
        }

        if (string.Equals(role.SystemKey, DefaultRoles.OfficeAdmin, StringComparison.Ordinal))
        {
            return OrganizationErrors.AdminRoleLocked;
        }

        if (await rules.RoleInUseAsync(role.Id, cancellationToken))
        {
            return OrganizationErrors.RoleInUse;
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

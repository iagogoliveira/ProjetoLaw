using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Domain.Members;
using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.Results;
using OrganizationEntity = LexFlow.Organization.Domain.Organizations.Organization;

namespace LexFlow.Organization.Application.Onboarding;

public sealed record OrganizationCreatedResponse(Guid Id, string Name);

/// <summary>
/// Onboarding (Etapa 3, tarefa 3): cria a organização, os perfis padrão e torna quem criou membro com o perfil
/// Administrador do escritório, escopo organização. Qualquer usuário autenticado pode criar um escritório (cadastro
/// aberto e fluxo e2e "cadastro → criar escritório", seção 13.2).
/// </summary>
public sealed record CreateOrganizationCommand(string Name) : ICommand<OrganizationCreatedResponse>;

internal sealed class CreateOrganizationValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithErrorCode("organization.name_required")
            .MaximumLength(OrganizationEntity.MaxNameLength).WithErrorCode("organization.name_too_long");
    }
}

internal sealed class CreateOrganizationHandler(
    IOrganizationDbContext db,
    ICurrentUser user,
    TenantOverride tenant,
    IAccessCache cache,
    IIdGenerator ids,
    IClock clock)
    : ICommandHandler<CreateOrganizationCommand, OrganizationCreatedResponse>
{
    public async Task<Result<OrganizationCreatedResponse>> Handle(CreateOrganizationCommand command, CancellationToken cancellationToken)
    {
        var userId = user.UserId ?? throw new InvalidOperationException("Onboarding exige usuário autenticado.");
        var organization = OrganizationEntity.Create(ids.NewId(), command.Name);

        // A organização acabou de nascer: ainda não há tenant ativo para os primeiros dados dela.
        using (tenant.BeginForNewOrganization(organization.Id))
        {
            db.Organizations.Add(organization);

            Role? admin = null;
            foreach (var definition in DefaultRoles.All)
            {
                var role = Role.Create(ids.NewId(), definition.Name, definition.Description, definition.SystemKey);
                role.SetPermissions(definition.Permissions);
                db.Roles.Add(role);
                admin ??= string.Equals(definition.SystemKey, DefaultRoles.OfficeAdmin, StringComparison.Ordinal) ? role : null;
            }

            var member = OrganizationMember.Join(ids.NewId(), organization.Id, userId, clock.UtcNow);
            db.Members.Add(member);
            db.UserOrganizations.Add(new UserOrganization(userId, organization.Id));
            db.RoleAssignments.Add(MemberRoleAssignment.Assign(ids.NewId(), member.Id, admin!.Id, DataScope.Organization).Value);

            await db.SaveChangesAsync(cancellationToken);
        }

        // A lista de organizações do criador mudou: a próxima requisição já enxerga o escritório novo.
        await cache.InvalidateMembershipsAsync(userId, cancellationToken);

        return new OrganizationCreatedResponse(organization.Id, organization.Name);
    }
}

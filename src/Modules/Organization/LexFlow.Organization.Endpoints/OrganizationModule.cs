using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Application.Access;
using LexFlow.Organization.Application.Onboarding;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Web.Common.Modules;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Organization.Endpoints;

public sealed record CreateOrganizationRequest(string Name);

public sealed record SelectActiveOrganizationRequest(Guid OrganizationId);

public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> Permissions);

public sealed record AssignMemberRolesRequest(IReadOnlyList<RoleAssignmentInput> Roles);

public sealed record MfaPolicyRequest(bool MfaRequired);

/// <summary>
/// Endpoints do módulo Organization (Etapa 3): onboarding, acesso do usuário e troca de organização, perfis e
/// atribuições, política de MFA. A organização é a ativa da requisição (<c>X-Organization-Id</c>, validado contra a
/// pertença). O cadastro de estrutura e os convites entram no PR 3D.
/// </summary>
public sealed class OrganizationModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var organizations = endpoints.MapGroup("/api/v1/organizations").WithTags("Organizations");

        organizations.MapPost("/", async (CreateOrganizationRequest request, ICommandHandler<CreateOrganizationCommand, OrganizationCreatedResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateOrganizationCommand(request.Name), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created((string?)null, created)))
            .RequirePermission(PermissionCatalog.OrganizationsCreate)
            .WithName("CreateOrganization")
            .WithSummary("Cria um escritório com os perfis padrão; quem cria vira Administrador do escritório")
            .Produces<OrganizationCreatedResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        MapAccess(endpoints);
        MapAdministration(endpoints);
    }

    private static void MapAccess(IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/api/v1/me").WithTags("Me");

        me.MapGet("/access", async (ICurrentUser user, IQueryHandler<GetMyAccessQuery, MyAccessResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetMyAccessQuery(user.UserId!.Value), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.ProfileRead)
            .WithName("GetMyAccess")
            .WithSummary("Organizações do usuário, a ativa e as permissões nela")
            .Produces<MyAccessResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        me.MapPost("/active-organization", async (SelectActiveOrganizationRequest request, ICurrentUser user, ICommandHandler<SelectActiveOrganizationCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new SelectActiveOrganizationCommand(user.UserId!.Value, request.OrganizationId), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(PermissionCatalog.ProfileManage)
            .WithName("SelectActiveOrganization")
            .WithSummary("Troca a organização usada quando a requisição não traz X-Organization-Id")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static void MapAdministration(IEndpointRouteBuilder endpoints)
    {
        var organization = endpoints.MapGroup("/api/v1/organization").WithTags("Organization");

        organization.MapGet("/members", async (IQueryHandler<ListMembersQuery, IReadOnlyList<MemberResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListMembersQuery(), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.SettingsUsersRead)
            .WithName("ListMembers")
            .WithSummary("Membros da organização ativa e os perfis de cada um")
            .Produces<IReadOnlyList<MemberResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        organization.MapPut("/members/{id:guid}/roles", async (Guid id, AssignMemberRolesRequest request, ICommandHandler<AssignMemberRolesCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new AssignMemberRolesCommand(id, request.Roles), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(PermissionCatalog.SettingsUsersManage)
            .WithName("AssignMemberRoles")
            .WithSummary("Substitui os perfis de um membro, com o escopo de dados de cada um")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        organization.MapPut("/security", async (MfaPolicyRequest request, ICommandHandler<SetMfaPolicyCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new SetMfaPolicyCommand(request.MfaRequired), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(PermissionCatalog.SettingsOrganizationManage)
            .WithName("SetMfaPolicy")
            .WithSummary("Exige (ou não) verificação em duas etapas de todos os membros")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var roles = endpoints.MapGroup("/api/v1/roles").WithTags("Roles");

        roles.MapGet("/", async (IQueryHandler<ListRolesQuery, IReadOnlyList<RoleResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListRolesQuery(), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.SettingsUsersRead)
            .WithName("ListRoles")
            .WithSummary("Perfis da organização ativa com as permissões")
            .Produces<IReadOnlyList<RoleResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        roles.MapPut("/{id:guid}/permissions", async (Guid id, UpdateRolePermissionsRequest request, ICommandHandler<UpdateRolePermissionsCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new UpdateRolePermissionsCommand(id, request.Permissions), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(PermissionCatalog.SettingsRolesManage)
            .WithName("UpdateRolePermissions")
            .WithSummary("Substitui as permissões de um perfil; vale para os membros em até 5 s, sem novo login")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}

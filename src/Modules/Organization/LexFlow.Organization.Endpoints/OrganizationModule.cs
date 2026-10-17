using LexFlow.Application.Abstractions.Messaging;
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

/// <summary>Endpoints do módulo Organization (Etapa 3). A estrutura, os membros e os perfis entram no PR 3D.</summary>
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
    }
}

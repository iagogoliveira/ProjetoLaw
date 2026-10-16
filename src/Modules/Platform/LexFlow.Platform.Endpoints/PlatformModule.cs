using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Application.Jobs;
using LexFlow.Web.Common.Modules;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Platform.Endpoints;

/// <summary>Permissões do módulo Platform (catálogo completo e perfis chegam na Etapa 3).</summary>
public static class PlatformPermissions
{
    public const string JobsRead = "platform.jobs.read";
}

/// <summary>Endpoints do módulo Platform (seção 4.2).</summary>
public sealed class PlatformModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var jobs = endpoints.MapGroup("/api/v1/jobs").WithTags("Jobs");

        jobs.MapGet("/{id:guid}", async (Guid id, IQueryHandler<GetJobQuery, JobResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetJobQuery(id), cancellationToken)).ToHttpResult())
            .RequirePermission(PlatformPermissions.JobsRead)
            .WithName("GetJob")
            .WithSummary("Estado de uma operação longa")
            .Produces<JobResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Application;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Modules;
using LexFlow.Web.Common.Pagination;
using LexFlow.Web.Common.Queries;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Publications.Endpoints;

public sealed record PublicationsPageResponse(IReadOnlyList<PublicationSummary> Items, string? NextCursor);
public sealed record MovementsPageResponse(IReadOnlyList<MovementResponse> Items, string? NextCursor);

public sealed class PublicationsModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) { }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        CaptureEndpoints.Map(endpoints);
        MapSources(endpoints);
        MapPublications(endpoints);
        MapMovements(endpoints);
    }

    private static void MapSources(IEndpointRouteBuilder endpoints)
    {
        var sources = endpoints.MapGroup("/api/v1").WithTags("Publications");
        sources.MapGet("/publication-sources", async (IQueryHandler<ListCaptureSourcesQuery, IReadOnlyList<CaptureSourceResponse>> handler, CancellationToken ct) =>
            (await handler.Handle(new ListCaptureSourcesQuery(), ct)).ToHttpResult())
            .RequirePermission("settings.integrations.manage").WithName("ListCaptureSources").Produces<IReadOnlyList<CaptureSourceResponse>>();
        sources.MapGet("/publication-sources/{id:guid}", async (Guid id, IQueryHandler<GetCaptureSourceQuery, CaptureSourceResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetCaptureSourceQuery(id), ct)).ToHttpResult())
            .RequirePermission("settings.integrations.manage").WithName("GetCaptureSource").Produces<CaptureSourceResponse>();
        sources.MapPost("/publication-sources", async (CaptureSourceInput input, ICommandHandler<CreateCaptureSourceCommand, CaptureSourceResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new CreateCaptureSourceCommand(input), ct)).ToHttpResult(s => TypedResults.Created($"/api/v1/publication-sources/{s.Id:D}", s)))
            .RequirePermission("settings.integrations.manage").WithName("CreateCaptureSource").Produces<CaptureSourceResponse>(StatusCodes.Status201Created);
        sources.MapPut("/publication-sources/{id:guid}", async (Guid id, CaptureSourceInput input, ICommandHandler<UpdateCaptureSourceCommand, CaptureSourceResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new UpdateCaptureSourceCommand(id, input), ct)).ToHttpResult())
            .RequirePermission("settings.integrations.manage").WithName("UpdateCaptureSource").Produces<CaptureSourceResponse>();
    }

    private static void MapPublications(IEndpointRouteBuilder endpoints)
    {
        var publications = endpoints.MapGroup("/api/v1").WithTags("Publications");
        publications.MapGet("/publications", async (string? status, bool? linked, Guid? caseId, string? cursor, int? limit,
            IQueryHandler<ListPublicationsQuery, PublicationsPage> handler, CancellationToken ct) =>
        {
            var parsedStatus = EnumQuery.Parse<PublicationStatus>(status, Error.Validation("publications.filter_invalid", "Filtro inválido."));
            if (parsedStatus.IsFailure) { return parsedStatus.Error.ToProblem(); }
            var position = cursor is null ? Result.Ok<PublicationPosition?>(null) : CursorCodec.Decode<PublicationPosition>(cursor).Map<PublicationPosition?>(p => p);
            if (position.IsFailure) { return position.Error.ToProblem(); }
            return (await handler.Handle(new ListPublicationsQuery(parsedStatus.Value, linked, caseId, position.Value, limit ?? 50), ct))
                .ToHttpResult(p => TypedResults.Ok(new PublicationsPageResponse(p.Items, p.Next is null ? null : CursorCodec.Encode(p.Next))));
        }).RequirePermission("publications.read").WithName("ListPublications").Produces<PublicationsPageResponse>().DocumentEnumQuery<PublicationStatus>("status");

        publications.MapGet("/publications/{id:guid}", async (Guid id, IQueryHandler<GetPublicationQuery, PublicationResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetPublicationQuery(id), ct)).ToHttpResult())
            .RequirePermission("publications.read").WithName("GetPublication").Produces<PublicationResponse>();
    }

    private static void MapMovements(IEndpointRouteBuilder endpoints)
    {
        var cases = endpoints.MapGroup("/api/v1/cases").WithTags("Publications");
        cases.MapGet("/{id:guid}/movements", async (Guid id, string? cursor, int? limit,
            IQueryHandler<ListCaseMovementsQuery, MovementsPage> handler, CancellationToken ct) =>
        {
            var position = cursor is null ? Result.Ok<MovementPosition?>(null) : CursorCodec.Decode<MovementPosition>(cursor).Map<MovementPosition?>(p => p);
            if (position.IsFailure) { return position.Error.ToProblem(); }
            return (await handler.Handle(new ListCaseMovementsQuery(id, position.Value, limit ?? 50), ct))
                .ToHttpResult(p => TypedResults.Ok(new MovementsPageResponse(p.Items, p.Next is null ? null : CursorCodec.Encode(p.Next))));
        }).RequirePermission("cases.read").WithName("ListCaseMovements").Produces<MovementsPageResponse>();

        cases.MapPost("/{id:guid}/movements", async (Guid id, MovementInput input,
            ICommandHandler<CreateCaseMovementCommand, MovementResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new CreateCaseMovementCommand(id, input), ct))
                .ToHttpResult(m => TypedResults.Created($"/api/v1/cases/{id:D}/movements", m)))
            .RequirePermission("cases.edit").WithName("CreateCaseMovement").Produces<MovementResponse>(StatusCodes.Status201Created);
    }
}

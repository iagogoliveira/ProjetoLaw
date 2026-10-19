using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Application.Closing;
using LexFlow.Publications.Application.Triage;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Publications.Endpoints;

public sealed record LinkPublicationInput(Guid CaseId);
public sealed record AssignPublicationInput(Guid? AssigneeUserId);

internal static class PublicationWorkflowEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/publications").WithTags("Publications");
        group.MapPut("/{id:guid}/link", async (Guid id, LinkPublicationInput input,
            ICommandHandler<LinkPublicationCommand, PublicationWorkflowResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new LinkPublicationCommand(id, input.CaseId), ct)).ToHttpResult())
            .RequirePermission("publications.triage").WithName("LinkPublication").Produces<PublicationWorkflowResponse>();
        group.MapPut("/{id:guid}/assignment", async (Guid id, AssignPublicationInput input,
            ICommandHandler<AssignPublicationCommand, PublicationWorkflowResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new AssignPublicationCommand(id, input.AssigneeUserId), ct)).ToHttpResult())
            .RequirePermission("publications.distribute").WithName("AssignPublication").Produces<PublicationWorkflowResponse>();
        group.MapPost("/{id:guid}/triage", async (Guid id, PublicationTriageInput input,
            ICommandHandler<TriagePublicationCommand, PublicationWorkflowResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new TriagePublicationCommand(id, input), ct)).ToHttpResult())
            .RequirePermission("publications.triage").WithName("TriagePublication").Produces<PublicationWorkflowResponse>();
        group.MapGet("/daily-close", async (DateOnly day, int? beforeRevision, int? limit,
            IQueryHandler<GetDailyCloseQuery, DailyCloseStatusResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new GetDailyCloseQuery(day, beforeRevision, limit ?? 50), ct)).ToHttpResult())
            .RequirePermission("publications.close").WithName("GetPublicationsDailyClose").Produces<DailyCloseStatusResponse>();
        group.MapPost("/daily-close", async (DailyCloseInput input,
            ICommandHandler<ClosePublicationsDayCommand, DailyCloseResponse> handler, CancellationToken ct) =>
            (await handler.Handle(new ClosePublicationsDayCommand(input.Day), ct)).ToHttpResult())
            .RequirePermission("publications.close").WithName("ClosePublicationsDay").Produces<DailyCloseResponse>();
    }
}

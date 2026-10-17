using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Organization.Application.Access;
using LexFlow.Organization.Application.Invitations;
using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Organization.Endpoints;

public sealed record CreateInvitationRequest(string Email, IReadOnlyList<RoleAssignmentInput> Roles);

/// <summary>Token recebido no link do convite. Vai no corpo, nunca na URL da API (não aparece em logs de acesso).</summary>
public sealed record InvitationTokenRequest(string Token);

/// <summary>
/// Convites (RF-01): o escritório convida, reenvia e cancela (organização ativa); quem recebeu o link consulta o
/// convite sem login e o aceita já autenticado, com a conta do e-mail convidado.
/// </summary>
internal static class InvitationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var office = endpoints.MapGroup("/api/v1/organization/invitations").WithTags("Invitations");

        office.MapGet("/", async (IQueryHandler<ListInvitationsQuery, IReadOnlyList<InvitationResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListInvitationsQuery(), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.SettingsUsersRead)
            .WithName("ListInvitations")
            .WithSummary("Convites da organização ativa, do mais recente ao mais antigo")
            .Produces<IReadOnlyList<InvitationResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        office.MapPost("/", async (CreateInvitationRequest request, ICommandHandler<CreateInvitationCommand, InvitationResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new CreateInvitationCommand(request.Email, request.Roles), cancellationToken))
                    .ToHttpResult(created => TypedResults.Created((string?)null, created)))
            .RequirePermission(PermissionCatalog.SettingsUsersManage)
            .WithName("CreateInvitation")
            .WithSummary("Convida por e-mail, com os perfis que o convidado recebe; o convite vale 7 dias")
            .Produces<InvitationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        office.MapPost("/{id:guid}/resend", async (Guid id, ICommandHandler<ResendInvitationCommand, InvitationResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ResendInvitationCommand(id), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.SettingsUsersManage)
            .WithName("ResendInvitation")
            .WithSummary("Reenvia um convite pendente: o prazo recomeça e o link anterior deixa de valer")
            .Produces<InvitationResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        office.MapDelete("/{id:guid}", async (Guid id, ICommandHandler<RevokeInvitationCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new RevokeInvitationCommand(id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(PermissionCatalog.SettingsUsersManage)
            .WithName("RevokeInvitation")
            .WithSummary("Cancela um convite pendente")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        MapRecipient(endpoints);
    }

    /// <summary>Quem recebeu o link: consulta sem login e aceite autenticado.</summary>
    private static void MapRecipient(IEndpointRouteBuilder endpoints)
    {
        var invitations = endpoints.MapGroup("/api/v1/invitations").WithTags("Invitations");

        invitations.MapPost("/preview", async (InvitationTokenRequest request, IQueryHandler<PreviewInvitationQuery, InvitationPreviewResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new PreviewInvitationQuery(request.Token), cancellationToken)).ToHttpResult())
            .AllowPublicAccess("Página do convite antes do login ou do cadastro de quem foi convidado")
            .RequireRateLimiting(PublicRateLimit.PolicyName)
            .WithName("PreviewInvitation")
            .WithSummary("Escritório, e-mail convidado e prazo de um convite válido")
            .Produces<InvitationPreviewResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        invitations.MapPost("/accept", async (InvitationTokenRequest request, ICommandHandler<AcceptInvitationCommand, InvitationAcceptedResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new AcceptInvitationCommand(request.Token), cancellationToken)).ToHttpResult())
            .RequirePermission(PermissionCatalog.InvitationsAccept)
            .WithName("AcceptInvitation")
            .WithSummary("Aceita o convite com a conta do e-mail convidado e entra no escritório com os perfis do convite")
            .Produces<InvitationAcceptedResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}

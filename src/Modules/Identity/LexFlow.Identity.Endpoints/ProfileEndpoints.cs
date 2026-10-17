using System.Security.Claims;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Mfa;
using LexFlow.Identity.Application.Profile;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LexFlow.Identity.Endpoints;

/// <summary>
/// Permissões do módulo Identity. São da própria conta: na Etapa 3, o <c>PermissionCatalog</c> deve concedê-las a
/// todo usuário autenticado, independentemente do perfil no escritório.
/// </summary>
public static class IdentityPermissions
{
    public const string ProfileRead = "identity.profile.read";
    public const string ProfileManage = "identity.profile.manage";
}

public sealed record ConfirmTotpRequest(string Code);

/// <summary>"Minha conta" (Etapa 2, tarefa 4): perfil, sessões abertas e ativação do segundo fator.</summary>
internal static class ProfileEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var me = endpoints.MapGroup("/api/v1/me").WithTags("Me");

        me.MapGet("/", async (ICurrentUser user, IQueryHandler<GetMeQuery, MeResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new GetMeQuery(user.UserId!.Value), cancellationToken)).ToHttpResult())
            .RequirePermission(IdentityPermissions.ProfileRead)
            .WithName("GetMe")
            .WithSummary("Perfil do usuário logado")
            .Produces<MeResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapGet("/sessions", async (ClaimsPrincipal principal, ICurrentUser user, IQueryHandler<ListMySessionsQuery, IReadOnlyList<SessionResponse>> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ListMySessionsQuery(user.UserId!.Value, CurrentSession(principal)), cancellationToken)).ToHttpResult())
            .RequirePermission(IdentityPermissions.ProfileRead)
            .WithName("ListMySessions")
            .WithSummary("Sessões abertas do usuário logado")
            .Produces<IReadOnlyList<SessionResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        me.MapDelete("/sessions/{id:guid}", async (Guid id, ICurrentUser user, ICommandHandler<EndMySessionCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new EndMySessionCommand(user.UserId!.Value, id), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(IdentityPermissions.ProfileManage)
            .WithName("EndMySession")
            .WithSummary("Encerra uma sessão do usuário logado e revoga os tokens dela")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        me.MapPost("/mfa/totp", async (ICurrentUser user, ICommandHandler<StartTotpSetupCommand, TotpSetupResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new StartTotpSetupCommand(user.UserId!.Value), cancellationToken)).ToHttpResult())
            .RequirePermission(IdentityPermissions.ProfileManage)
            .WithName("StartTotpSetup")
            .WithSummary("Gera a chave do aplicativo autenticador (o MFA só liga na confirmação)")
            .Produces<TotpSetupResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);

        me.MapPost("/mfa/totp/confirm", async (ConfirmTotpRequest request, ICurrentUser user, ICommandHandler<ConfirmTotpCommand, RecoveryCodesResponse> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new ConfirmTotpCommand(user.UserId!.Value, request.Code), cancellationToken)).ToHttpResult())
            .RequirePermission(IdentityPermissions.ProfileManage)
            .WithName("ConfirmTotp")
            .WithSummary("Confirma o aplicativo autenticador com um código e liga o MFA; devolve os códigos de recuperação")
            .Produces<RecoveryCodesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        me.MapPost("/mfa/email", async (ICurrentUser user, ICommandHandler<EnableEmailMfaCommand, Unit> handler, CancellationToken cancellationToken) =>
                (await handler.Handle(new EnableEmailMfaCommand(user.UserId!.Value), cancellationToken)).ToHttpResult(_ => TypedResults.NoContent()))
            .RequirePermission(IdentityPermissions.ProfileManage)
            .WithName("EnableEmailMfa")
            .WithSummary("Liga o código por e-mail como segundo fator")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static Guid? CurrentSession(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(LexFlowClaims.SessionId), out var id) ? id : null;
}

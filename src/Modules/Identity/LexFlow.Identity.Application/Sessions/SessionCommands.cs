using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Domain;
using LexFlow.Identity.Domain.Sessions;
using LexFlow.Identity.Domain.Users;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Identity.Application.Sessions;

/// <summary>Motivos gravados em <c>sessions.revoked_reason</c>.</summary>
public static class SessionEndReasons
{
    public const string Logout = "logout";
    public const string PasswordReset = "password_reset";
}

/// <summary>Abre a sessão do login, ligada à autorização OIDC que emite os tokens dela. Retorna o id da sessão (claim <c>sid</c>).</summary>
public sealed record StartSessionCommand(Guid UserId, string AuthorizationId, string? IpAddress, string? UserAgent) : ICommand<Guid>;

internal sealed class StartSessionValidator : AbstractValidator<StartSessionCommand>
{
    public StartSessionValidator()
    {
        RuleFor(c => c.UserId).NotEmpty().WithErrorCode("identity.user_id_required");
        RuleFor(c => c.AuthorizationId).NotEmpty().WithErrorCode("identity.authorization_id_required");
    }
}

internal sealed class StartSessionHandler(IIdentityDbContext db, UserManager<ApplicationUser> users, IIdGenerator ids, IClock clock)
    : ICommandHandler<StartSessionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(StartSessionCommand command, CancellationToken cancellationToken)
    {
        if (await users.FindByIdAsync(command.UserId.ToString()) is not { } user)
        {
            return IdentityErrors.UserNotFound;
        }

        var now = clock.UtcNow;
        var session = Session.Start(ids.NewId(), user.Id, command.IpAddress, command.UserAgent, now);
        session.AttachAuthorization(command.AuthorizationId);
        db.Sessions.Add(session);
        user.RecordLogin(now);

        // UpdateAsync grava o usuário e a sessão no mesmo SaveChanges (mesmo contexto).
        var updated = await users.UpdateAsync(user);
        return updated.Succeeded ? session.Id : IdentityErrors.UserNotFound;
    }
}

/// <summary>
/// Renovação de token: a sessão precisa estar ativa (não revogada, dentro de 14 dias sem uso e de 30 dias de idade)
/// e pertencer ao usuário do token. Em sucesso, a janela de inatividade recomeça.
/// </summary>
public sealed record RenewSessionCommand(Guid SessionId, Guid UserId) : ICommand;

internal sealed class RenewSessionHandler(IIdentityDbContext db, IClock clock) : ICommandHandler<RenewSessionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RenewSessionCommand command, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken);
        if (session is null || session.UserId != command.UserId || !session.IsActive(now))
        {
            return IdentityErrors.SessionExpired;
        }

        session.Touch(now);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>Encerra a sessão e revoga os tokens dela. Repetir não tem efeito (logout duplo, sessão já expirada).</summary>
public sealed record EndSessionCommand(Guid SessionId, string Reason) : ICommand;

internal sealed class EndSessionHandler(IIdentityDbContext db, ISessionTokens tokens, IClock clock) : ICommandHandler<EndSessionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(EndSessionCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == command.SessionId, cancellationToken);
        if (session is null)
        {
            return IdentityErrors.SessionNotFound;
        }

        await SessionTermination.EndAsync([session], command.Reason, db, tokens, clock, cancellationToken);
        return Unit.Value;
    }
}

internal static class SessionTermination
{
    /// <summary>Encerra todas as sessões ainda não revogadas do usuário (ex.: senha redefinida).</summary>
    public static async Task EndAllAsync(Guid userId, string reason, IIdentityDbContext db, ISessionTokens tokens, IClock clock, CancellationToken cancellationToken)
    {
        var sessions = await db.Sessions.Where(s => s.UserId == userId && s.RevokedAt == null).ToListAsync(cancellationToken);
        await EndAsync(sessions, reason, db, tokens, clock, cancellationToken);
    }

    public static async Task EndAsync(IReadOnlyList<Session> sessions, string reason, IIdentityDbContext db, ISessionTokens tokens, IClock clock, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        foreach (var session in sessions)
        {
            session.Revoke(now, reason);
            if (session.AuthorizationId is { } authorizationId)
            {
                await tokens.RevokeAsync(authorizationId, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

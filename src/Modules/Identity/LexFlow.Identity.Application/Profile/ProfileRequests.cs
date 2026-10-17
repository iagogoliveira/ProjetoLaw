using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Application.Sessions;
using LexFlow.Identity.Domain;
using LexFlow.Identity.Domain.Users;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Identity.Application.Profile;

/// <summary>Perfil do usuário logado. <c>mfaMethod</c>: <c>none</c>, <c>totp</c> ou <c>email</c>.</summary>
public sealed record MeResponse(
    Guid Id,
    string Email,
    string FullName,
    bool EmailConfirmed,
    string MfaMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

public sealed record GetMeQuery(Guid UserId) : IQuery<MeResponse>;

internal sealed class GetMeHandler(UserManager<ApplicationUser> users) : IQueryHandler<GetMeQuery, MeResponse>
{
    public async Task<Result<MeResponse>> Handle(GetMeQuery query, CancellationToken cancellationToken) =>
        await users.FindByIdAsync(query.UserId.ToString()) is { } user
            ? new MeResponse(user.Id, user.Email!, user.FullName, user.EmailConfirmed, user.MfaMethod.ToString().ToLowerInvariant(), user.CreatedAt, user.LastLoginAt)
            : IdentityErrors.UserNotFound;
}

/// <summary>Sessão aberta. <c>current</c> marca a do token que fez a consulta.</summary>
public sealed record SessionResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    bool Current);

public sealed record ListMySessionsQuery(Guid UserId, Guid? CurrentSessionId) : IQuery<IReadOnlyList<SessionResponse>>;

/// <summary>
/// Só sessões que ainda podem ser usadas: não revogadas, dentro dos prazos e com refresh token válido (um reuso
/// detectado revoga os tokens sem passar pela sessão). A mais recente vem primeiro.
/// </summary>
internal sealed class ListMySessionsHandler(IIdentityDbContext db, ISessionTokens tokens, IClock clock)
    : IQueryHandler<ListMySessionsQuery, IReadOnlyList<SessionResponse>>
{
    public async Task<Result<IReadOnlyList<SessionResponse>>> Handle(ListMySessionsQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.UserId == query.UserId && s.RevokedAt == null)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync(cancellationToken);

        var open = new List<SessionResponse>();
        foreach (var session in sessions.Where(s => s.IsActive(now)))
        {
            if (session.AuthorizationId is { } authorization && await tokens.HasActiveRefreshTokenAsync(authorization, cancellationToken))
            {
                open.Add(new SessionResponse(session.Id, session.CreatedAt, session.LastSeenAt, session.ExpiresAt, session.IpAddress, session.UserAgent, session.Id == query.CurrentSessionId));
            }
        }

        return open;
    }
}

/// <summary>Encerra uma sessão do próprio usuário (ex.: outro dispositivo). Sessão de outra pessoa dá 404.</summary>
public sealed record EndMySessionCommand(Guid UserId, Guid SessionId) : ICommand;

internal sealed class EndMySessionHandler(IIdentityDbContext db, ISessionTokens tokens, IClock clock) : ICommandHandler<EndMySessionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(EndMySessionCommand command, CancellationToken cancellationToken)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == command.SessionId && s.UserId == command.UserId, cancellationToken);
        if (session is null)
        {
            return IdentityErrors.SessionNotFound;
        }

        await SessionTermination.EndAsync([session], SessionEndReasons.Logout, db, tokens, clock, cancellationToken);
        return Unit.Value;
    }
}

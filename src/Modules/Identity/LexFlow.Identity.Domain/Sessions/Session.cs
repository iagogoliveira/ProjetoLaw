using LexFlow.SharedKernel.Domain;

namespace LexFlow.Identity.Domain.Sessions;

/// <summary>
/// Sessão de login (seção 7.1, <c>identity.sessions</c>): criada no login e ligada à autorização OIDC, cujos tokens são
/// revogados quando a sessão é encerrada. Expira por inatividade (renovação a cada uso) e por idade máxima.
/// </summary>
public sealed class Session : Entity<Guid>
{
    /// <summary>Sem uso por este período, a sessão expira (decisão de 2026-10-16).</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromDays(14);

    /// <summary>Idade máxima: depois disso é preciso entrar de novo.</summary>
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(30);

    public const int MaxUserAgentLength = 512;

    private Session()
    {
    }

    private Session(Guid id, Guid userId, string? ipAddress, string? userAgent, DateTimeOffset now)
        : base(id)
    {
        UserId = userId;
        IpAddress = ipAddress;
        UserAgent = userAgent is { Length: > MaxUserAgentLength } ? userAgent[..MaxUserAgentLength] : userAgent;
        CreatedAt = now;
        LastSeenAt = now;
    }

    public Guid UserId { get; private set; }

    /// <summary>Identificador da autorização do OpenIddict (dona dos tokens desta sessão).</summary>
    public string? AuthorizationId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    public DateTimeOffset ExpiresAt => Min(LastSeenAt + IdleTimeout, CreatedAt + AbsoluteLifetime);

    public static Session Start(Guid id, Guid userId, string? ipAddress, string? userAgent, DateTimeOffset now) =>
        new(id, userId, ipAddress, userAgent, now);

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void AttachAuthorization(string authorizationId) => AuthorizationId = authorizationId;

    /// <summary>Registra uso (renovação de token); não reabre sessão revogada ou expirada.</summary>
    public void Touch(DateTimeOffset now)
    {
        if (IsActive(now))
        {
            LastSeenAt = now;
        }
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is null)
        {
            RevokedAt = now;
            RevokedReason = reason;
        }
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}

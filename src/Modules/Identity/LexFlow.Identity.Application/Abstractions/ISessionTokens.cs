namespace LexFlow.Identity.Application.Abstractions;

/// <summary>Tokens OIDC de uma sessão (implementado sobre o OpenIddict).</summary>
public interface ISessionTokens
{
    /// <summary>Revoga a autorização da sessão e todos os tokens emitidos por ela.</summary>
    Task RevokeAsync(string authorizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Se ainda há refresh token válido. Falso depois de um reuso detectado (o OpenIddict revoga a família) ou de
    /// expirar a janela de inatividade: a sessão não pode mais ser renovada, mesmo sem <c>revoked_at</c>.
    /// </summary>
    Task<bool> HasActiveRefreshTokenAsync(string authorizationId, CancellationToken cancellationToken);
}

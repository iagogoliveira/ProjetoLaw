namespace LexFlow.Identity.Application.Abstractions;

/// <summary>Revoga a autorização OIDC de uma sessão e todos os tokens emitidos por ela (implementado sobre o OpenIddict).</summary>
public interface ISessionTokenRevoker
{
    Task RevokeAsync(string authorizationId, CancellationToken cancellationToken);
}

namespace LexFlow.Identity.Infrastructure;

/// <summary>Configuração do módulo (seção <c>Identity</c>).</summary>
public sealed class IdentitySettings
{
    public const string SectionName = "Identity";

    /// <summary>Endereço público do servidor de identidade, base dos links enviados por e-mail.</summary>
    public Uri PublicUrl { get; init; } = new("http://localhost:5000");

    /// <summary>
    /// Verificação de senhas vazadas na API Have I Been Pwned (k-anonymity): ligada por padrão (decisão de 2026-10-16).
    /// Desligue onde não houver acesso à internet.
    /// </summary>
    public bool CheckBreachedPasswords { get; init; } = true;

    public OidcServerSettings Server { get; init; } = new();

    public WebClientSettings WebClient { get; init; } = new();
}

/// <summary>
/// Chaves do servidor OIDC. Em produção, certificados PFX em Base64 vindos do cofre (nunca do repositório). Sem eles,
/// só em Development: certificados de desenvolvimento do OpenIddict, ou chaves efêmeras (testes).
/// </summary>
public sealed class OidcServerSettings
{
    public string? SigningCertificate { get; init; }

    public string? EncryptionCertificate { get; init; }

    public string? CertificatePassword { get; init; }

    /// <summary>Chaves só em memória: tokens deixam de valer quando o processo reinicia. Para testes.</summary>
    public bool UseEphemeralKeys { get; init; }
}

/// <summary>Cliente <c>lexflow-web</c> (SPA, Authorization Code + PKCE).</summary>
public sealed class WebClientSettings
{
    public const string ClientId = "lexflow-web";

    /// <summary>Sem valor configurado, vale o endereço do <c>ng serve</c> local.</summary>
    public IReadOnlyList<Uri>? RedirectUris { get; init; }

    public IReadOnlyList<Uri>? PostLogoutRedirectUris { get; init; }

    public IReadOnlyList<Uri> EffectiveRedirectUris => RedirectUris is { Count: > 0 } uris ? uris : [new Uri("http://localhost:4200/auth/callback")];

    public IReadOnlyList<Uri> EffectivePostLogoutRedirectUris => PostLogoutRedirectUris is { Count: > 0 } uris ? uris : [new Uri("http://localhost:4200/")];
}

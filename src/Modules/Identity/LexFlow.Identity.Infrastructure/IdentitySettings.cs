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
}

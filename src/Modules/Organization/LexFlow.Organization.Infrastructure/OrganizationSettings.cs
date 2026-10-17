namespace LexFlow.Organization.Infrastructure;

/// <summary>Configuração do módulo (seção <c>Organization</c>).</summary>
public sealed class OrganizationSettings
{
    public const string SectionName = "Organization";

    /// <summary>Endereço público do SPA, base do link dos convites (<c>/convite?token=…</c>).</summary>
    public Uri AppUrl { get; init; } = new("http://localhost:4200/");
}

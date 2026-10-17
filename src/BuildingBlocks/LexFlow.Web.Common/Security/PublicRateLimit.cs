namespace LexFlow.Web.Common.Security;

/// <summary>
/// Política de limite por IP dos endpoints públicos (seção 7.3: 10 req/min), registrada pelo módulo Identity e usada
/// por qualquer módulo que exponha endpoint sem login (ex.: a consulta do convite).
/// </summary>
public static class PublicRateLimit
{
    public const string PolicyName = "public";
}

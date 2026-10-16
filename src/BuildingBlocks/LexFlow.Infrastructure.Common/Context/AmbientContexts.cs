using LexFlow.Application.Abstractions;

namespace LexFlow.Infrastructure.Common.Context;

/// <summary>
/// Tenant e correlação de processos fora de HTTP (consumidores e jobs no Worker), definidos por escopo assíncrono a
/// partir da mensagem: <c>using (AmbientContext.Begin(evento.OrganizationId, evento.CorrelationId)) { ... }</c>.
/// </summary>
public static class AmbientContext
{
    private static readonly AsyncLocal<Frame?> Current = new();

    internal static Guid? OrganizationId => Current.Value?.OrganizationId;

    internal static string? CorrelationId => Current.Value?.CorrelationId;

    /// <summary>Sem organização (<c>null</c>): processo da plataforma, sem acesso a dados de escritório.</summary>
    public static IDisposable Begin(Guid? organizationId, string? correlationId)
    {
        var previous = Current.Value;
        Current.Value = new Frame(organizationId, correlationId);
        return new Restore(previous);
    }

    private sealed record Frame(Guid? OrganizationId, string? CorrelationId);

    private sealed class Restore(Frame? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

/// <summary>Tenant do escopo ambiente; sem escopo, nenhum (dados de escritório invisíveis, gravações rejeitadas).</summary>
internal sealed class AmbientTenantContext : ITenantContext
{
    public Guid? OrganizationId => AmbientContext.OrganizationId;
}

internal sealed class AmbientRequestContext : IRequestContext
{
    public string? CorrelationId => AmbientContext.CorrelationId;

    public string? IpAddress => null;

    public string? UserAgent => null;
}

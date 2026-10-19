using LexFlow.SharedKernel.Domain;

namespace LexFlow.Publications.Domain;

/// <summary>Preserva as ativações para o fechamento não perder capturas esperadas após uma desativação.</summary>
public sealed class CaptureSourcePeriod : Entity<Guid>, ITenantOwned
{
    private CaptureSourcePeriod() { }
    public Guid OrganizationId { get; private set; }
    public Guid SourceId { get; private set; }
    public DateTimeOffset ActivatedAt { get; private set; }
    public DateTimeOffset? DeactivatedAt { get; private set; }

    public static CaptureSourcePeriod Start(Guid sourceId, DateTimeOffset activatedAt) => new()
    { Id = Guid.CreateVersion7(), SourceId = sourceId, ActivatedAt = activatedAt.ToUniversalTime() };

    public void End(DateTimeOffset now) => DeactivatedAt ??= now.ToUniversalTime();
}

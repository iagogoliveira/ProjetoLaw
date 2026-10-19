using LexFlow.SharedKernel.Domain;

namespace LexFlow.Publications.Domain;

/// <summary>Uma situação de captura ausente por fonte/dia; novas verificações não criam cópias.</summary>
public sealed class CaptureAlert : Entity<Guid>, ITenantOwned
{
    private CaptureAlert() { }
    public Guid OrganizationId { get; private set; }
    public Guid SourceId { get; private set; }
    public DateOnly Day { get; private set; }
    public DateTimeOffset RaisedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string ErrorCode { get; private set; } = string.Empty;

    public static CaptureAlert Raise(Guid sourceId, DateOnly day, DateTimeOffset now, string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        if (sourceId == Guid.Empty || day == default || errorCode.Length > 100) { throw new ArgumentException("Dados do alerta inválidos.", nameof(sourceId)); }
        return new CaptureAlert { Id = Guid.CreateVersion7(), SourceId = sourceId, Day = day, RaisedAt = now.ToUniversalTime(), ErrorCode = errorCode };
    }
    public bool Reopen(DateTimeOffset now, string errorCode)
    {
        if (ResolvedAt is null) { return false; }
        RaisedAt = now.ToUniversalTime();
        ErrorCode = errorCode;
        ResolvedAt = null;
        return true;
    }
    public void Resolve(DateTimeOffset now) => ResolvedAt ??= now.ToUniversalTime();
}

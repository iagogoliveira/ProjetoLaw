using LexFlow.SharedKernel.Domain;

namespace LexFlow.Platform.Domain.Idempotency;

/// <summary>
/// Chave <c>Idempotency-Key</c> de um POST sensível (seção 4.4), guardada por 24 h com o hash da requisição e a
/// resposta, para que a repetição devolva a mesma resposta sem executar a operação de novo.
/// </summary>
[NotAudited]
public sealed class IdempotencyKey : Entity<Guid>, ITenantOwned
{
    public const int MaxKeyLength = 128;

    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private IdempotencyKey()
    {
    }

    private IdempotencyKey(Guid id, string key, string requestHash, DateTimeOffset now)
        : base(id)
    {
        Key = key;
        RequestHash = requestHash;
        CreatedAt = now;
        ExpiresAt = now + Retention;
    }

    public Guid OrganizationId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    /// <summary>SHA-256 de método, rota e corpo, em hexadecimal.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    /// <summary>Nulo enquanto a primeira requisição ainda está em andamento.</summary>
    public int? ResponseStatusCode { get; private set; }

    public string? ResponseContentType { get; private set; }

    public string? ResponseBody { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsCompleted => ResponseStatusCode.HasValue;

    public static IdempotencyKey Begin(Guid id, string key, string requestHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);
        return new IdempotencyKey(id, key, requestHash, now);
    }

    public void Complete(int statusCode, string? contentType, string? body)
    {
        ResponseStatusCode = statusCode;
        ResponseContentType = contentType;
        ResponseBody = body;
    }
}

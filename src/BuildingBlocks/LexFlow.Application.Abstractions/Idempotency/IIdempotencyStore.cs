namespace LexFlow.Application.Abstractions.Idempotency;

/// <summary>Resposta gravada da primeira execução, devolvida nas repetições com a mesma chave.</summary>
public sealed record StoredResponse(int StatusCode, string? ContentType, string? Body);

public enum IdempotencyOutcome
{
    /// <summary>Primeira vez: a operação deve ser executada.</summary>
    Started,

    /// <summary>Já concluída com o mesmo conteúdo: devolver a resposta gravada.</summary>
    Replay,

    /// <summary>A primeira requisição com essa chave ainda está em andamento.</summary>
    InProgress,

    /// <summary>A chave já foi usada com outro conteúdo.</summary>
    KeyReused,
}

public sealed record IdempotencyBegin(IdempotencyOutcome Outcome, StoredResponse? Response = null);

/// <summary>Chaves <c>Idempotency-Key</c> por organização, guardadas por 24 h (seção 4.4; tabela <c>platform.idempotency_keys</c>).</summary>
public interface IIdempotencyStore
{
    Task<IdempotencyBegin> BeginAsync(string key, string requestHash, CancellationToken cancellationToken);

    Task CompleteAsync(string key, StoredResponse response, CancellationToken cancellationToken);

    /// <summary>Libera a chave após falha inesperada (5xx ou exceção), permitindo nova tentativa.</summary>
    Task AbandonAsync(string key, CancellationToken cancellationToken);
}

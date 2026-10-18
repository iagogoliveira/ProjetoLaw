namespace LexFlow.Platform.Contracts.Jobs;

/// <summary>
/// Operações longas acompanhadas por <c>GET /api/v1/jobs/{id}</c> (seção 4.4), para os outros módulos: quem recebe o
/// pedido cria o job e responde 202 com o <c>Location</c>; quem processa (o Worker) marca o início e o fim. Cada
/// chamada grava na hora. Implementado pelo Platform.
/// </summary>
public interface IJobTracker
{
    /// <summary>Cria o job na fila (ex.: tipo <c>cases.import.validate</c>) e devolve o id.</summary>
    Task<Guid> CreateAsync(string type, CancellationToken cancellationToken);

    Task StartAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>Concluído, com o recurso produzido (ex.: <c>/api/v1/cases/imports/{id}</c>).</summary>
    Task SucceedAsync(Guid jobId, string? resultLocation, CancellationToken cancellationToken);

    /// <summary>Falhou, com o código estável do erro.</summary>
    Task FailAsync(Guid jobId, string errorCode, CancellationToken cancellationToken);
}

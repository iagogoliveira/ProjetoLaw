namespace LexFlow.Platform.Contracts.Courts;

/// <summary>Tribunal da tabela de referência (segmento J e código TR do número CNJ).</summary>
public sealed record CourtInfo(Guid Id, int Segment, string Code, string Acronym, string Name, string? State);

/// <summary>Tribunais da plataforma (dados de referência, iguais para todos os escritórios). Implementado pelo Platform.</summary>
public interface ICourtDirectory
{
    /// <summary>O tribunal do segmento (J) e do código (TR) de um número CNJ, se existir.</summary>
    Task<CourtInfo?> FindAsync(int segment, string code, CancellationToken cancellationToken);

    Task<CourtInfo?> GetAsync(Guid id, CancellationToken cancellationToken);
}

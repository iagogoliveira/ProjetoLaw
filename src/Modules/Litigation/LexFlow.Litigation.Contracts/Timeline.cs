namespace LexFlow.Litigation.Contracts;

/// <summary>
/// Um acontecimento na linha do tempo do processo. <see cref="Kind"/> é um código estável (<c>modulo.evento</c>, ex.:
/// <c>litigation.hearing_rescheduled</c>) que a tela traduz; <see cref="Data"/> traz os valores para o texto.
/// <see cref="Id"/> é único e estável entre as consultas (ex.: <c>hearing:{id}:rescheduled:{n}</c>), para a paginação.
/// </summary>
public sealed record TimelineEntry(string Id, DateTimeOffset At, string Kind, IReadOnlyDictionary<string, string?> Data, Guid? ReferenceId);

/// <summary>
/// Fonte de acontecimentos da linha do tempo do processo (seção 10, Etapa 5, tarefa 4). Cada módulo com dados ligados
/// a processos registra a sua (prazos, documentos, financeiro...); o Litigation junta todas. É chamada depois de o
/// usuário ter o processo ao alcance.
/// </summary>
public interface ITimelineContributor
{
    /// <summary>Os acontecimentos do processo anteriores à posição informada, do mais recente ao mais antigo, até o limite.</summary>
    Task<IReadOnlyList<TimelineEntry>> GetAsync(Guid caseId, TimelinePosition? before, int limit, CancellationToken cancellationToken);
}

/// <summary>Posição na linha do tempo: a data e, para desempatar, o id do acontecimento.</summary>
public sealed record TimelinePosition(DateTimeOffset At, string Id)
{
    /// <summary>Ordem da linha do tempo: do mais recente ao mais antigo; no empate, pelo id em ordem decrescente.</summary>
    public static int Compare(DateTimeOffset at, string id, TimelinePosition other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var byDate = other.At.CompareTo(at);
        return byDate != 0 ? byDate : string.CompareOrdinal(other.Id, id);
    }

    /// <summary>Se o acontecimento vem depois desta posição na leitura (é mais antigo).</summary>
    public bool Precedes(TimelineEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Compare(entry.At, entry.Id, this) > 0;
    }
}

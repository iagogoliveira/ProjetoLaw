using System.Globalization;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Cases;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Litigation.Application.Timeline;

public sealed record TimelinePage(IReadOnlyList<TimelineEntry> Items, TimelinePosition? Next);

/// <summary>
/// Linha do tempo do processo (Etapa 5, tarefa 4): os acontecimentos de todos os módulos (<see cref="ITimelineContributor"/>),
/// do mais recente ao mais antigo, paginados por posição. <see cref="Kinds"/> filtra pelo começo do código (ex.:
/// <c>litigation.hearing</c>).
/// </summary>
public sealed record GetCaseTimelineQuery(Guid CaseId, TimelinePosition? After, int Limit, IReadOnlyList<string>? Kinds) : IQuery<TimelinePage>;

internal sealed class GetCaseTimelineHandler(CaseWrites cases, IEnumerable<ITimelineContributor> contributors) : IQueryHandler<GetCaseTimelineQuery, TimelinePage>
{
    public async Task<Result<TimelinePage>> Handle(GetCaseTimelineQuery query, CancellationToken cancellationToken)
    {
        if (!await cases.ExistsAsync(query.CaseId, cancellationToken))
        {
            return LitigationErrors.CaseNotFound;
        }

        // Cada fonte devolve até o limite (mais um, para saber se há mais) depois da posição; a página é o topo da junção.
        var all = new List<TimelineEntry>();
        foreach (var contributor in contributors)
        {
            all.AddRange(await contributor.GetAsync(query.CaseId, query.After, query.Limit + 1, cancellationToken));
        }

        var ordered = all
            .Where(e => query.Kinds is not { Count: > 0 } kinds || kinds.Any(k => e.Kind.StartsWith(k, StringComparison.Ordinal)))
            .OrderByDescending(e => e.At).ThenByDescending(e => e.Id, StringComparer.Ordinal)
            .ToList();
        var page = ordered.Take(query.Limit).ToList();
        var next = ordered.Count > query.Limit ? new TimelinePosition(page[^1].At, page[^1].Id) : null;
        return new TimelinePage(page, next);
    }
}

/// <summary>Formatos estáveis dos valores na linha do tempo (a tela formata para o idioma).</summary>
internal static class TimelineValues
{
    private static readonly TimeSpan SaoPaulo = TimeSpan.FromHours(-3);

    public static string? Of(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture);

    public static string? Of(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Of(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static string Of(Guid value) => value.ToString("D");

    /// <summary>Data sem hora (ex.: a homologação) como o início do dia em São Paulo.</summary>
    public static DateTimeOffset At(DateOnly date) => new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), SaoPaulo).ToUniversalTime();
}

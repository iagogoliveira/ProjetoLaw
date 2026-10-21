using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Platform.Contracts.Control;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Application.Control;

public sealed record GetControlReportQuery(Guid UserId, bool MultiFactorAuthenticated, DateOnly? From,
    DateOnly? Until, int IdleDays = 30, int Limit = 50) : IQuery<ControlReportResponse>;
public sealed record ControlReportResponse(DateTimeOffset AsOf, DateOnly From, DateOnly Until, int IdleDays,
    IReadOnlyList<ControlSection> Sections, IReadOnlyList<ControlMetric> Metrics);

internal sealed class GetControlReportHandler(IEnumerable<IControlReportSource> sources, IClock clock)
    : IQueryHandler<GetControlReportQuery, ControlReportResponse>
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<Result<ControlReportResponse>> Handle(GetControlReportQuery query, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);
        var from = query.From ?? today.AddDays(-30);
        var until = query.Until ?? today.AddDays(1);
        if (query.UserId == Guid.Empty || query.IdleDays is < 1 or > 3650 || query.Limit is < 1 or > 100
            || from == default || until <= from || until.DayNumber - from.DayNumber > 366)
        {
            return Error.Validation("control.invalid_query", "Informe intervalo de até 366 dias, limite de 1 a 100 e inatividade de 1 a 3650 dias.");
        }
        var window = new ControlWindow(query.UserId, query.MultiFactorAuthenticated, now, Start(from), Start(until), query.IdleDays, query.Limit);
        var sections = new List<ControlSection>();
        var metrics = new List<ControlMetric>();
        foreach (var source in sources)
        {
            var result = await source.ReadAsync(window, cancellationToken);
            sections.AddRange(result.Sections);
            metrics.AddRange(result.Metrics);
        }
        return new ControlReportResponse(now, from, until, query.IdleDays, sections, metrics);
    }

    private static DateTimeOffset Start(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        while (Zone.IsInvalidTime(local)) { local = local.AddMinutes(1); }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone));
    }
}

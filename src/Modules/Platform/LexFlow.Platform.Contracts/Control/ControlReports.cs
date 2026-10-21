namespace LexFlow.Platform.Contracts.Control;

public static class ControlPermissions
{
    public const string Read = "schedule.control.read";
}

public sealed record ControlWindow(Guid UserId, bool MultiFactorAuthenticated, DateTimeOffset AsOf,
    DateTimeOffset From, DateTimeOffset Until, int IdleDays, int Limit);
public sealed record ControlItem(Guid Id, Guid? CaseId, string Label, DateTimeOffset At);
public sealed record ControlMetric(string Kind, decimal? Value, string Unit, long SampleSize);
public sealed record ControlSection(string Kind, long Count, IReadOnlyList<ControlItem> Items);
public sealed record ControlSourceResult(IReadOnlyList<ControlSection> Sections, IReadOnlyList<ControlMetric> Metrics);

/// <summary>Consulta do módulo dono, com autorização aplicada antes das linhas e dos totais.</summary>
public interface IControlReportSource
{
    Task<ControlSourceResult> ReadAsync(ControlWindow window, CancellationToken cancellationToken);
}

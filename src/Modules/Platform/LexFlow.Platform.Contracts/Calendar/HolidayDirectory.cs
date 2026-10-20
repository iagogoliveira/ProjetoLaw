namespace LexFlow.Platform.Contracts.Calendar;

/// <summary>Um dia sem contagem vindo do calendário da plataforma: feriado (sem expediente) ou suspensão de prazos.</summary>
public sealed record PlatformNonWorkingDay(DateOnly Date, string Name, bool IsSuspension, string LegalBasis);

/// <summary>Calendário forense mantido pela plataforma (dados de referência, iguais para todos os escritórios).</summary>
public interface IHolidayDirectory
{
    /// <summary>
    /// Os dias sem contagem no intervalo (inclusive): nacionais e suspensões do CPC sempre; os estaduais da
    /// <paramref name="stateCode"/>, se informada; os da Justiça Federal, se <paramref name="federalJustice"/>.
    /// </summary>
    Task<IReadOnlyList<PlatformNonWorkingDay>> ForAsync(DateOnly fromDate, DateOnly toDate, string? stateCode, bool federalJustice,
        CancellationToken cancellationToken);
}

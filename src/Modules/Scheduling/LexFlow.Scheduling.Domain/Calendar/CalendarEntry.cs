using System.Globalization;
using System.Text;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Domain.Calendar;

public enum CalendarEntryKind
{
    /// <summary>Feriado municipal ou forense próprio (sem expediente).</summary>
    Holiday,

    /// <summary>Suspensão do curso dos prazos determinada pelo tribunal.</summary>
    Suspension,

    /// <summary>Indisponibilidade do sistema do tribunal (CPC, art. 224, § 1º): prorroga início e vencimento.</summary>
    SystemOutage,
}

public enum CalendarEntryScope
{
    /// <summary>Vale para todos os processos do escritório.</summary>
    Office,

    /// <summary>Vale para os processos de uma UF.</summary>
    State,

    /// <summary>Vale para os processos de uma comarca (UF e município).</summary>
    Municipality,

    /// <summary>Vale para os processos de um tribunal.</summary>
    Court,
}

public sealed record CalendarEntryInput(string Name, CalendarEntryKind Kind, CalendarEntryScope Scope, string? StateCode, string? Municipality,
    Guid? CourtId, DateOnly StartsOn, DateOnly EndsOn, bool RecursYearly, string? Note);

/// <summary>
/// Dia ou período sem contagem cadastrado pelo escritório, complementando o calendário da plataforma: feriados municipais,
/// forenses próprios de um tribunal ou comarca, suspensões e indisponibilidades de sistema.
/// </summary>
public sealed class CalendarEntry : Entity<Guid>, ITenantOwned, IAuditable
{
    public const int MaxSpanDays = 366;
    public const int MaxRecurringSpanDays = 62;

    private static readonly HashSet<string> States = new(StringComparer.Ordinal)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SE", "SP", "TO",
    };

    private CalendarEntry() { }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public CalendarEntryKind Kind { get; private set; }
    public CalendarEntryScope Scope { get; private set; }
    public string? StateCode { get; private set; }
    public string? Municipality { get; private set; }
    public Guid? CourtId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }

    /// <summary>Repete todo ano nas mesmas datas (ex.: aniversário do município).</summary>
    public bool RecursYearly { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static Result<CalendarEntry> Create(CalendarEntryInput input)
    {
        var entry = new CalendarEntry { Id = Guid.CreateVersion7() };
        var result = entry.Update(input);
        return result.IsFailure ? result.Error : entry;
    }

    public Result Update(CalendarEntryInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IsValid(input)) { return SchedulingErrors.InvalidCalendarEntry; }

        Name = input.Name.Trim();
        Kind = input.Kind;
        Scope = input.Scope;
        StateCode = input.Scope is CalendarEntryScope.State or CalendarEntryScope.Municipality ? input.StateCode!.Trim().ToUpperInvariant() : null;
        Municipality = input.Scope == CalendarEntryScope.Municipality ? CollapseSpaces(input.Municipality!) : null;
        CourtId = input.Scope == CalendarEntryScope.Court ? input.CourtId : null;
        StartsOn = input.StartsOn;
        EndsOn = input.EndsOn;
        RecursYearly = input.RecursYearly;
        Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        return Result.Success;
    }

    /// <summary>Se o registro vale para um processo com essa localização.</summary>
    public bool AppliesTo(string? stateCode, string? municipality, Guid? courtId) => Scope switch
    {
        CalendarEntryScope.Office => true,
        CalendarEntryScope.State => string.Equals(StateCode, stateCode, StringComparison.OrdinalIgnoreCase),
        CalendarEntryScope.Municipality => string.Equals(StateCode, stateCode, StringComparison.OrdinalIgnoreCase)
            && municipality is not null && string.Equals(MunicipalityKey(Municipality!), MunicipalityKey(municipality), StringComparison.Ordinal),
        CalendarEntryScope.Court => courtId is not null && CourtId == courtId,
        _ => false,
    };

    /// <summary>Os dias do registro dentro do intervalo, repetindo a cada ano se for recorrente.</summary>
    public IEnumerable<NonWorkingDay> DaysBetween(DateOnly from, DateOnly to)
    {
        var suspension = Kind == CalendarEntryKind.Suspension;
        var reason = Kind == CalendarEntryKind.SystemOutage ? $"Indisponibilidade: {Name}" : Name;
        // Recorrente: repete a partir do ano cadastrado; a ocorrência do ano anterior pode avançar no intervalo.
        var shifts = RecursYearly
            ? Enumerable.Range(from.Year - 1, to.Year - from.Year + 2).Select(year => year - StartsOn.Year).Where(shift => shift >= 0)
            : [0];
        foreach (var shift in shifts)
        {
            var start = StartsOn.AddYears(shift);
            var end = EndsOn.AddYears(shift);
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                if (day >= from && day <= to) { yield return new NonWorkingDay(day, reason, suspension); }
            }
        }
    }

    /// <summary>Chave de comparação do município: sem acentos, sem diferença de caixa e com espaços simples.</summary>
    public static string MunicipalityKey(string municipality)
    {
        ArgumentNullException.ThrowIfNull(municipality);
        var decomposed = CollapseSpaces(municipality).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    private static string CollapseSpaces(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static bool IsValid(CalendarEntryInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 200 || (input.Note?.Trim().Length ?? 0) > 500
            || !Enum.IsDefined(input.Kind) || !Enum.IsDefined(input.Scope)
            || input.StartsOn == default || input.EndsOn < input.StartsOn)
        {
            return false;
        }

        var span = input.EndsOn.DayNumber - input.StartsOn.DayNumber + 1;
        if (span > (input.RecursYearly ? MaxRecurringSpanDays : MaxSpanDays)) { return false; }

        var state = input.StateCode?.Trim().ToUpperInvariant();
        return input.Scope switch
        {
            CalendarEntryScope.Office => input.StateCode is null && input.Municipality is null && input.CourtId is null,
            CalendarEntryScope.State => state is not null && States.Contains(state) && input.Municipality is null && input.CourtId is null,
            CalendarEntryScope.Municipality => state is not null && States.Contains(state) && !string.IsNullOrWhiteSpace(input.Municipality)
                && input.Municipality.Trim().Length <= 120 && input.CourtId is null,
            CalendarEntryScope.Court => input.CourtId is { } id && id != Guid.Empty && input.StateCode is null && input.Municipality is null,
            _ => false,
        };
    }
}

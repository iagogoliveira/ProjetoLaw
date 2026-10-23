using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Application.CalendarSync;

public sealed record CalendarSyncContext(Guid OrganizationId, Guid UserId, CalendarProvider Provider);
public sealed record RemoteCalendarEvent(string Id, string Version, string Title, string? Description, string? Location,
    DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateOnly? Day, bool IsCancelled, bool IsRecurring, string? ClientKey);
public sealed record RemoteCalendarPage(IReadOnlyList<RemoteCalendarEvent> Items, string? NextCursor);
/// <summary>Dados mínimos de saída. Nunca contém título, descrição, local ou dados do processo.</summary>
public sealed record CalendarSyncWrite(string ClientKey, CalendarItemKind Kind, DateTimeOffset StartsAt,
    DateTimeOffset EndsAt, DateOnly? Day, bool IsCancelled, string Link);

public interface ICalendarSyncProvider
{
    bool IsEnabled { get; }
    bool IsSimulation { get; }
    Task<Result<RemoteCalendarPage>> ReadAsync(CalendarSyncContext context, DateOnly from, DateOnly until,
        string? cursor, CancellationToken cancellationToken);
    Task<Result<RemoteCalendarEvent?>> GetAsync(CalendarSyncContext context, string remoteId, CancellationToken cancellationToken);
    /// <summary>Criação idempotente por ClientKey; edição condicional por versão. Preserva textos remotos existentes.</summary>
    Task<Result<RemoteCalendarEvent>> WriteAsync(CalendarSyncContext context, CalendarSyncWrite item,
        string? remoteId, string? expectedVersion, CancellationToken cancellationToken);
}

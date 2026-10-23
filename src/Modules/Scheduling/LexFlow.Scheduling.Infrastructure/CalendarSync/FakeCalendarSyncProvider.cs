using System.Globalization;
using LexFlow.Scheduling.Application.CalendarSync;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Infrastructure.CalendarSync;

/// <summary>Configuração do provedor de calendário simulado.</summary>
public sealed class FakeCalendarSyncOptions
{
    public bool Enabled { get; set; }
    public bool Available { get; set; } = true;
    public int PageSize { get; set; } = 100;
}

/// <summary>Simulador em memória isolado por escritório, usuário e provedor.</summary>
public sealed class FakeCalendarSyncProvider(FakeCalendarSyncOptions options) : ICalendarSyncProvider
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "E. South America Standard Time" : "America/Sao_Paulo");
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<Scope, List<RemoteCalendarEvent>> _events = [];
    private readonly Dictionary<Scope, Dictionary<string, string>> _links = [];

    public bool IsEnabled => options.Enabled;
    public bool IsSimulation => true;

    /// <summary>Insere dados normalizados para cenários de teste do simulador.</summary>
    public void Seed(CalendarSyncContext context, IReadOnlyList<RemoteCalendarEvent> events)
    {
        if (!ValidContext(context)) { throw new ArgumentException("Contexto de calendário inválido.", nameof(context)); }
        lock (_gate) _events[ToScope(context)] = events.ToList();
    }

    /// <summary>Retorna uma cópia dos dados do simulador para cenários de teste.</summary>
    public IReadOnlyList<RemoteCalendarEvent> Snapshot(CalendarSyncContext context)
    {
        if (!ValidContext(context)) { throw new ArgumentException("Contexto de calendário inválido.", nameof(context)); }
        lock (_gate) return _events.TryGetValue(ToScope(context), out var events) ? events.ToArray() : [];
    }

    public Task<Result<RemoteCalendarPage>> ReadAsync(CalendarSyncContext context, DateOnly from, DateOnly until,
        string? cursor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsEnabled || !options.Available) return Task.FromResult(Result.Failure<RemoteCalendarPage>(CalendarSyncErrors.ProviderUnavailable));
        if (!ValidWindow(context, from, until) || options.PageSize < 1)
            return Task.FromResult(Result.Failure<RemoteCalendarPage>(CalendarSyncErrors.InvalidConnection));
        var offset = 0;
        if (cursor is not null && (!int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset < 0))
            return Task.FromResult(Result.Failure<RemoteCalendarPage>(CalendarSyncErrors.InvalidRemote));

        lock (_gate)
        {
            IEnumerable<RemoteCalendarEvent> source = _events.TryGetValue(ToScope(context), out var events)
                ? events : Enumerable.Empty<RemoteCalendarEvent>();
            var selected = source.Where(item => Overlaps(item, from, until))
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
            if (offset > selected.Length) return Task.FromResult(Result.Failure<RemoteCalendarPage>(CalendarSyncErrors.InvalidRemote));
            var page = selected.Skip(offset).Take(options.PageSize).ToArray();
            var next = offset + page.Length < selected.Length
                ? (offset + page.Length).ToString(CultureInfo.InvariantCulture) : null;
            return Task.FromResult(Result.Ok(new RemoteCalendarPage(page, next)));
        }
    }

    public Task<Result<RemoteCalendarEvent?>> GetAsync(CalendarSyncContext context, string remoteId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsEnabled || !options.Available)
            return Task.FromResult(Result.Failure<RemoteCalendarEvent?>(CalendarSyncErrors.ProviderUnavailable));
        if (!ValidContext(context) || string.IsNullOrWhiteSpace(remoteId))
            return Task.FromResult(Result.Failure<RemoteCalendarEvent?>(CalendarSyncErrors.InvalidRemote));
        lock (_gate)
        {
            var found = _events.TryGetValue(ToScope(context), out var events)
                ? events.FirstOrDefault(item => string.Equals(item.Id, remoteId, StringComparison.Ordinal)) : null;
            return Task.FromResult(Result.Ok(found));
        }
    }

    public Task<Result<RemoteCalendarEvent>> WriteAsync(CalendarSyncContext context, CalendarSyncWrite item,
        string? remoteId, string? expectedVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsEnabled || !options.Available) return Task.FromResult(Result.Failure<RemoteCalendarEvent>(CalendarSyncErrors.ProviderUnavailable));
        if (!ValidContext(context) || !ValidWrite(item))
            return Task.FromResult(Result.Failure<RemoteCalendarEvent>(CalendarSyncErrors.InvalidRemote));

        lock (_gate)
        {
            var scope = ToScope(context);
            if (!_events.TryGetValue(scope, out var events)) _events[scope] = events = [];
            if (!_links.TryGetValue(scope, out var links)) _links[scope] = links = new(StringComparer.Ordinal);

            if (remoteId is null)
            {
                var existing = events.FirstOrDefault(x => string.Equals(x.ClientKey, item.ClientKey, StringComparison.Ordinal));
                if (existing is not null) return Task.FromResult(Result.Ok(existing));
                var created = new RemoteCalendarEvent(Guid.NewGuid().ToString("N"), "1", GenericTitle(item.Kind), null,
                    null, item.StartsAt.ToUniversalTime(), item.EndsAt.ToUniversalTime(), item.Day, item.IsCancelled, false, item.ClientKey);
                events.Add(created);
                links[created.Id] = item.Link;
                return Task.FromResult(Result.Ok(created));
            }

            var index = events.FindIndex(x => string.Equals(x.Id, remoteId, StringComparison.Ordinal));
            if (index < 0)
                return Task.FromResult(Result.Failure<RemoteCalendarEvent>(expectedVersion is null
                    ? CalendarSyncErrors.NotFound : CalendarSyncErrors.RemoteConflict));
            var current = events[index];
            if (expectedVersion is null || !string.Equals(current.Version, expectedVersion, StringComparison.Ordinal))
                return Task.FromResult(Result.Failure<RemoteCalendarEvent>(CalendarSyncErrors.RemoteConflict));
            var updated = current with
            {
                Version = NextVersion(current.Version),
                StartsAt = item.StartsAt.ToUniversalTime(),
                EndsAt = item.EndsAt.ToUniversalTime(),
                Day = item.Day,
                IsCancelled = item.IsCancelled,
                IsRecurring = false
            };
            events[index] = updated;
            links[updated.Id] = item.Link;
            return Task.FromResult(Result.Ok(updated));
        }
    }

    private static bool ValidContext(CalendarSyncContext context) => context is not null && context.OrganizationId != Guid.Empty
        && context.UserId != Guid.Empty && Enum.IsDefined(context.Provider);

    private static bool ValidWindow(CalendarSyncContext context, DateOnly from, DateOnly until) => ValidContext(context)
        && from != default && until.DayNumber - from.DayNumber is >= 1 and <= 366;

    private static bool ValidWrite(CalendarSyncWrite item) => item is not null && !string.IsNullOrWhiteSpace(item.ClientKey)
        && !string.IsNullOrWhiteSpace(item.Link) && Enum.IsDefined(item.Kind) && item.EndsAt > item.StartsAt
        && (item.Day is null || item.Day != default);

    private static Scope ToScope(CalendarSyncContext context) => new(context.OrganizationId, context.UserId, context.Provider);
    private static string GenericTitle(CalendarItemKind kind) => kind == CalendarItemKind.Deadline ? "Prazo LexFlow" : "Compromisso LexFlow";
    private static string NextVersion(string version) => int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
        ? checked(value + 1).ToString(CultureInfo.InvariantCulture) : string.Concat(version, "+1");

    private static bool Overlaps(RemoteCalendarEvent item, DateOnly from, DateOnly until)
    {
        if (item.Day is { } day) return day >= from && day < until;
        var first = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), SaoPaulo);
        var last = TimeZoneInfo.ConvertTimeToUtc(until.ToDateTime(TimeOnly.MinValue), SaoPaulo);
        return item.StartsAt.UtcDateTime < last && item.EndsAt.UtcDateTime > first;
    }

    private sealed record Scope(Guid OrganizationId, Guid UserId, CalendarProvider Provider);
}

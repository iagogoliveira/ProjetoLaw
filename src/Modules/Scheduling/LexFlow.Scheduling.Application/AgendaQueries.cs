using System.Text;
using System.Text.Json;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Litigation.Contracts;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Application;

public enum AgendaKind { Deadline, Hearing, Event, Task }
public sealed record AgendaItem(AgendaKind Kind, Guid Id, Guid? CaseId, string Title, DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt, bool AllDay, Guid? AssigneeUserId, string Status, DateOnly? InternalDueOn);
public sealed record AgendaPage(IReadOnlyList<AgendaItem> Items, string? NextCursor);
public sealed record ListAgendaQuery(DateOnly From, DateOnly Until, Guid? AssigneeUserId, Guid? TeamId,
    bool IncludeClosed, string? Cursor, int Limit = 50) : IQuery<AgendaPage>;
internal sealed record AgendaCursor(DateTimeOffset At, AgendaKind Kind, Guid Id);

internal static class AgendaTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static DateTimeOffset Start(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        // Historical DST can skip midnight: the civil day starts at its first valid instant.
        while (Zone.IsInvalidTime(local)) { local = local.AddMinutes(1); }
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone));
    }
    public static DateOnly Day(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);
}

internal sealed class ListAgendaHandler(AgendaSources sources, AgendaFilters filters, IHearingCalendar hearings) : IQueryHandler<ListAgendaQuery, AgendaPage>
{
    public async Task<Result<AgendaPage>> Handle(ListAgendaQuery query, CancellationToken ct)
    {
        if (query.From == default || query.Until <= query.From || query.Until.DayNumber - query.From.DayNumber > 366
            || query.Limit is < 1 or > 100) { return SchedulingErrors.InvalidAgenda; }
        var cursor = Parse(query.Cursor);
        if (cursor.IsFailure) { return cursor.Error; }
        var users = await filters.UsersAsync(query.AssigneeUserId, query.TeamId, ct);
        var items = new List<AgendaItem>();
        items.AddRange(await sources.DeadlinesAsync(query, cursor.Value, users, ct));
        items.AddRange(await sources.EventsAsync(query, cursor.Value, users, ct));
        items.AddRange(await sources.TasksAsync(query, cursor.Value, users, ct));
        var after = cursor.Value;
        var hearingItems = await hearings.ListAsync(new HearingCalendarWindow(AgendaTime.Start(query.From), AgendaTime.Start(query.Until), users,
            query.IncludeClosed, after?.At, after?.Kind == AgendaKind.Hearing ? after.Id : null, after is null || AgendaKind.Hearing >= after.Kind, query.Limit + 1), ct);
        items.AddRange(hearingItems.Select(h => new AgendaItem(AgendaKind.Hearing, h.Id, h.CaseId, h.Type, h.ScheduledAt, null,
            false, h.LawyerUserId, h.Status, null)));
        var page = items.OrderBy(i => i.StartsAt).ThenBy(i => i.Kind).ThenBy(i => i.Id).Take(query.Limit + 1).ToArray();
        var last = page.Take(query.Limit).LastOrDefault();
        var next = page.Length > query.Limit && last is not null
            ? Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new AgendaCursor(last.StartsAt, last.Kind, last.Id)))) : null;
        return new AgendaPage(page.Take(query.Limit).ToArray(), next);
    }

    private static Result<AgendaCursor?> Parse(string? value)
    {
        if (value is null) { return (AgendaCursor?)null; }
        if (value.Length > 512) { return SchedulingErrors.InvalidAgenda; }
        try
        {
            var cursor = JsonSerializer.Deserialize<AgendaCursor>(Convert.FromBase64String(value));
            if (cursor is null || cursor.Id == Guid.Empty || !Enum.IsDefined(cursor.Kind) || cursor.At == default) { return SchedulingErrors.InvalidAgenda; }
            return cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException) { return SchedulingErrors.InvalidAgenda; }
    }
}

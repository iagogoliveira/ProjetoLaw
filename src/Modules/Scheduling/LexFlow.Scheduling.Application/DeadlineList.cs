using System.Text;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Scheduling.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

public sealed record DeadlineListItem(Guid Id, Guid CaseId, string Type, string Title, DateOnly StartOn,
    DateOnly InternalDueOn, DateOnly FatalDueOn, Guid AssigneeUserId, WorkItemStatus Status,
    bool RequireReview, DateTimeOffset? ReviewedAt);
public sealed record DeadlineListPage(DateOnly Today, IReadOnlyList<DeadlineListItem> Items, string? NextCursor);
public sealed record ListDeadlinesQuery(DateOnly From, DateOnly Until, Guid? AssigneeUserId, Guid? TeamId,
    Guid? CaseId, bool IncludeClosed, string? Cursor, int Limit = 50) : IQuery<DeadlineListPage>;
internal sealed record DeadlineListCursor(DateOnly Day, Guid Id);

internal sealed class ListDeadlinesHandler(ISchedulingDbContext db, AgendaAccess access, AgendaFilters filters, IClock clock)
    : IQueryHandler<ListDeadlinesQuery, DeadlineListPage>
{

    public async Task<Result<DeadlineListPage>> Handle(ListDeadlinesQuery query, CancellationToken ct)
    {
        if (query.From == default || query.Until <= query.From || query.Until.DayNumber - query.From.DayNumber > 366
            || query.Limit is < 1 or > 100) { return SchedulingErrors.InvalidDeadlineList; }
        var cursor = Parse(query.Cursor);
        if (cursor.IsFailure) { return cursor.Error; }
        var scope = await access.ScopeAsync("schedule.deadlines.read", ct);
        var users = await filters.UsersAsync(query.AssigneeUserId, query.TeamId, ct);
        var source = db.Deadlines.AsNoTracking().Where(d => scope.Cases.Contains(d.LegalCaseId)
            && d.FatalDueOn >= query.From && d.FatalDueOn < query.Until);
        if (query.CaseId is { } caseId) { source = source.Where(d => d.LegalCaseId == caseId); }
        if (users is not null) { source = source.Where(d => users.Contains(d.AssigneeUserId)); }
        if (!query.IncludeClosed) { source = source.Where(d => d.Status == WorkItemStatus.Pending || d.Status == WorkItemStatus.InProgress); }
        if (cursor.Value is { } after)
        {
            source = source.Where(d => d.FatalDueOn > after.Day || (d.FatalDueOn == after.Day && d.Id.CompareTo(after.Id) > 0));
        }
        var rows = await source.OrderBy(d => d.FatalDueOn).ThenBy(d => d.Id).Take(query.Limit + 1)
            .Select(d => new DeadlineListItem(d.Id, d.LegalCaseId, d.Type, d.Title, d.StartOn, d.InternalDueOn, d.FatalDueOn,
                d.AssigneeUserId, d.Status, d.RequireReview, d.ReviewedAt)).ToArrayAsync(ct);
        var items = rows.Take(query.Limit).ToArray();
        var next = rows.Length > query.Limit ? Encode(items[^1]) : null;
        return new DeadlineListPage(AgendaTime.Day(clock.UtcNow), items, next);
    }

    private static string Encode(DeadlineListItem item) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new DeadlineListCursor(item.FatalDueOn, item.Id))));

    private static Result<DeadlineListCursor?> Parse(string? value)
    {
        if (value is null) { return (DeadlineListCursor?)null; }
        if (value.Length > 256) { return SchedulingErrors.InvalidDeadlineList; }
        try
        {
            var cursor = JsonSerializer.Deserialize<DeadlineListCursor>(Convert.FromBase64String(value));
            return cursor is null || cursor.Id == Guid.Empty || cursor.Day == default ? SchedulingErrors.InvalidDeadlineList : cursor;
        }
        catch (Exception exception) when (exception is FormatException or JsonException) { return SchedulingErrors.InvalidDeadlineList; }
    }
}

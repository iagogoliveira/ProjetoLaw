using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Scheduling.Application.CalendarSync;

public sealed record CalendarSyncSettings(string PublicWebUrl);
internal sealed record LocalCalendarSnapshot(Guid Id, Guid? CaseId, CalendarItemKind Kind, string Title,
    string? Description, string? Location, DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateOnly? Day, bool IsCancelled, string Fingerprint);

internal static class CalendarSyncSnapshots
{
    public static string Json(RemoteCalendarEvent item) => JsonSerializer.Serialize(item);
    public static RemoteCalendarEvent? Read(string json) => JsonSerializer.Deserialize<RemoteCalendarEvent>(json);
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static string Fingerprint(CalendarEvent item) => Hash(new
    {
        item.LegalCaseId,
        item.AssigneeUserId,
        item.Title,
        item.Description,
        item.Location,
        item.StartsAt,
        item.EndsAt,
        item.Status
    });
    public static string Fingerprint(Deadline item) => Hash(new { item.LegalCaseId, item.AssigneeUserId, item.FatalDueOn, item.Status });

    public static Result<CalendarEventInput> Input(RemoteCalendarEvent remote, Guid userId, CalendarEvent? local, RemoteCalendarEvent? baseline)
    {
        if (!Valid(remote) || remote.Day is not null || remote.IsRecurring) { return CalendarSyncErrors.InvalidRemote; }
        var title = local is null || baseline is null || !string.Equals(remote.Title, baseline.Title, StringComparison.Ordinal) ? remote.Title : local.Title;
        var description = local is null || baseline is null || !string.Equals(remote.Description, baseline.Description, StringComparison.Ordinal) ? remote.Description : local.Description;
        var location = local is null || baseline is null || !string.Equals(remote.Location, baseline.Location, StringComparison.Ordinal) ? remote.Location : local.Location;
        return new CalendarEventInput(local?.LegalCaseId, title, description, remote.StartsAt, remote.EndsAt, userId, location);
    }

    public static bool Valid(RemoteCalendarEvent item) => !string.IsNullOrWhiteSpace(item.Id) && item.Id.Length <= 200
        && !string.IsNullOrWhiteSpace(item.Version) && item.Version.Length <= 200 && !string.IsNullOrWhiteSpace(item.Title)
        && item.Title.Length <= 300 && (item.Description?.Length ?? 0) <= 4000 && (item.Location?.Length ?? 0) <= 500
        && item.StartsAt != default && item.EndsAt > item.StartsAt;
}

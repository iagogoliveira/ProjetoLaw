using System.Net;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class DeadlineLifecycleTests
{
    [Fact]
    public async Task Confirmed_dates_and_snapshot_stay_fixed_while_relevant_calendar_changes_alert_and_update_preview()
    {
        var office = await OfficeAsync();
        var entry = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/schedule/calendar-entries", CalendarEntryBody("2027-01-15"));
        entry.Status.ShouldBe(HttpStatusCode.Created, entry.Body.ToString());
        var entryId = entry.Body.GetProperty("id").GetGuid();
        var dates = await DatesAsync(office, triggerDate: "2027-01-11", days: 5, internalLeadDays: 1, stateCode: "SP", municipality: "São Paulo");
        var request = Request(office.Admin, dates, triggerDate: "2027-01-11", days: 5, internalLeadDays: 1, stateCode: "SP", municipality: "São Paulo");
        var created = await CreateDeadlineAsync(office, request);
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var id = created.Body.GetProperty("id").GetGuid();
        var originalDue = created.Body.GetProperty("fatalDueOn").GetString();
        var originalCalculation = created.Body.GetProperty("calculation").GetRawText();

        var irrelevant = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/schedule/calendar-entries/{entryId}", CalendarEntryBody("2027-01-15", note: "Editorial note only"));
        irrelevant.Status.ShouldBe(HttpStatusCode.OK, irrelevant.Body.ToString());
        var (_, afterIrrelevant) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/deadlines/{id}");
        afterIrrelevant.GetProperty("fatalDueOn").GetString().ShouldBe(originalDue);
        afterIrrelevant.GetProperty("calculation").GetRawText().ShouldBe(originalCalculation);
        afterIrrelevant.GetProperty("lastCalendarAlertAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);

        var relevant = await SendAsync(office.Admin, HttpMethod.Put, $"/api/v1/schedule/calendar-entries/{entryId}", CalendarEntryBody("2027-01-25", note: "Moved outside the confirmed calculation period"));
        relevant.Status.ShouldBe(HttpStatusCode.OK, relevant.Body.ToString());
        var (detailStatus, afterRelevant) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/deadlines/{id}");
        var updatedPreview = await CalculateAsync(office, triggerDate: "2027-01-11", days: 5, internalLeadDays: 1, stateCode: "SP", municipality: "São Paulo");
        var (_, timeline) = await SendAsync(office.Admin, HttpMethod.Get,
            $"/api/v1/cases/{office.CaseId}/timeline?kind=scheduling.calendar_changed");

        detailStatus.ShouldBe(HttpStatusCode.OK);
        afterRelevant.GetProperty("fatalDueOn").GetString().ShouldBe(originalDue, "the confirmed human dates must never be recalculated");
        afterRelevant.GetProperty("calculation").GetRawText().ShouldBe(originalCalculation);
        afterRelevant.GetProperty("lastCalendarAlertAt").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.String);
        afterRelevant.GetProperty("lastCalendarAlertAt").GetDateTimeOffset().Offset.ShouldBe(TimeSpan.Zero);
        updatedPreview.Status.ShouldBe(HttpStatusCode.OK, updatedPreview.Body.ToString());
        updatedPreview.Body.GetProperty("fatalDueOn").GetString().ShouldNotBe(originalDue);
        timeline.GetProperty("items").GetArrayLength().ShouldBe(1);
        timeline.GetProperty("items")[0].GetProperty("kind").GetString().ShouldBe("scheduling.calendar_changed");
        timeline.GetProperty("items")[0].GetProperty("data").GetProperty("calendarEntryId").GetString().ShouldBe(entryId.ToString());
        timeline.GetProperty("items")[0].GetProperty("data").TryGetProperty("note", out _).ShouldBeFalse();
    }

    private static object CalendarEntryBody(string day, string? note = null) => new
    {
        name = "Prazo sem expediente de teste",
        kind = "holiday",
        scope = "municipality",
        stateCode = "SP",
        municipality = "São Paulo",
        courtId = (Guid?)null,
        startsOn = day,
        endsOn = day,
        recursYearly = false,
        note,
    };
}

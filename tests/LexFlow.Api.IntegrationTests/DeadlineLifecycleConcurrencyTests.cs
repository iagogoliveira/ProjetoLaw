using System.Net;
using System.Text.Json;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class DeadlineLifecycleTests
{
    [Fact]
    public async Task Concurrent_completion_closes_once_and_records_only_one_activity()
    {
        var office = await OfficeAsync();
        var id = await RegisteredDeadlineAsync(office);
        var path = $"/api/v1/deadlines/{id}/complete";

        var responses = await Task.WhenAll(
            SendAsync(office.Admin, HttpMethod.Post, path, new { evidence = "protocol A" }),
            SendAsync(office.Admin, HttpMethod.Post, path, new { evidence = "protocol B" }));

        responses.Select(response => response.Status).OrderBy(status => status)
            .ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        var (getStatus, detail) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/deadlines/{id}");
        var (_, timeline) = await SendAsync(office.Admin, HttpMethod.Get,
            $"/api/v1/cases/{office.CaseId}/timeline?kind=scheduling.deadline_completed");

        getStatus.ShouldBe(HttpStatusCode.OK);
        detail.GetProperty("status").GetString().ShouldBe("completed");
        detail.GetProperty("completionEvidence").GetString().ShouldBeOneOf("protocol A", "protocol B");
        timeline.GetProperty("items").GetArrayLength().ShouldBe(1);
        timeline.GetProperty("items")[0].GetProperty("kind").GetString().ShouldBe("scheduling.deadline_completed");
    }

    [Fact]
    public async Task Adding_and_removing_a_calendar_entry_alerts_without_rewriting_confirmed_deadline_data()
    {
        var office = await OfficeAsync();
        var dates = await DatesAsync(office, triggerDate: "2027-03-01", days: 5, internalLeadDays: 1,
            stateCode: "SP", municipality: "São Paulo");
        var created = await CreateDeadlineAsync(office, Request(office.Admin, dates, triggerDate: "2027-03-01", days: 5,
            internalLeadDays: 1, stateCode: "SP", municipality: "São Paulo"));
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var deadlineId = created.Body.GetProperty("id").GetGuid();
        var confirmedDue = created.Body.GetProperty("fatalDueOn").GetString();
        var calculationSnapshot = created.Body.GetProperty("calculation").GetRawText();

        var added = await SendAsync(office.Admin, HttpMethod.Post, "/api/v1/schedule/calendar-entries", OfficeCalendarEntryBody);
        added.Status.ShouldBe(HttpStatusCode.Created, added.Body.ToString());
        var entryId = added.Body.GetProperty("id").GetGuid();
        var (_, afterAdd) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/deadlines/{deadlineId}");
        afterAdd.GetProperty("fatalDueOn").GetString().ShouldBe(confirmedDue);
        afterAdd.GetProperty("calculation").GetRawText().ShouldBe(calculationSnapshot);
        afterAdd.GetProperty("lastCalendarAlertAt").ValueKind.ShouldBe(JsonValueKind.String);

        var removed = await SendAsync(office.Admin, HttpMethod.Delete, $"/api/v1/schedule/calendar-entries/{entryId}");
        removed.Status.ShouldBe(HttpStatusCode.NoContent, removed.Body.ToString());
        var (_, afterRemove) = await SendAsync(office.Admin, HttpMethod.Get, $"/api/v1/deadlines/{deadlineId}");
        var (_, timeline) = await SendAsync(office.Admin, HttpMethod.Get,
            $"/api/v1/cases/{office.CaseId}/timeline?kind=scheduling.calendar_changed");

        afterRemove.GetProperty("fatalDueOn").GetString().ShouldBe(confirmedDue);
        afterRemove.GetProperty("calculation").GetRawText().ShouldBe(calculationSnapshot);
        afterRemove.GetProperty("lastCalendarAlertAt").ValueKind.ShouldBe(JsonValueKind.String);
        var alerts = timeline.GetProperty("items").EnumerateArray().ToList();
        alerts.Count.ShouldBe(2);
        alerts.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count().ShouldBe(2);
        alerts.ShouldAllBe(item => string.Equals(item.GetProperty("kind").GetString(), "scheduling.calendar_changed", StringComparison.Ordinal));
        alerts.ShouldAllBe(item => string.Equals(item.GetProperty("data").GetProperty("calendarEntryId").GetString(), entryId.ToString(), StringComparison.Ordinal));
        foreach (var item in alerts)
        {
            var data = item.GetProperty("data");
            data.TryGetProperty("completionEvidence", out _).ShouldBeFalse();
            data.TryGetProperty("missedReason", out _).ShouldBeFalse();
        }
    }

    private static object OfficeCalendarEntryBody => new
    {
        name = "Suspensão extraordinária de teste",
        kind = "holiday",
        scope = "office",
        stateCode = (string?)null,
        municipality = (string?)null,
        courtId = (Guid?)null,
        startsOn = "2027-03-02",
        endsOn = "2027-03-02",
        recursYearly = false,
        note = (string?)null,
    };
}

using System.Text.Json;
using LexFlow.Scheduling.Application.CalendarSync;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.Scheduling.Infrastructure.CalendarSync;

namespace LexFlow.Scheduling.IntegrationTests;

public sealed class CalendarSyncProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] GoogleWindowEventIds = ["google-event-a"];
    private static readonly Guid OrganizationId = Guid.Parse("019a0000-0000-7000-8000-000000000001");
    private static readonly Guid UserId = Guid.Parse("019a0000-0000-7000-8000-000000000002");

    [Theory]
    [InlineData(CalendarProvider.Google, "calendar-sync-google.json")]
    [InlineData(CalendarProvider.Microsoft365, "calendar-sync-microsoft365.json")]
    public async Task Reads_normalized_fixture_with_stable_paging_and_keeps_tombstones(CalendarProvider provider, string fixture)
    {
        var sut = Enabled(pageSize: 1);
        var context = Context(provider);
        sut.Seed(context, ReadFixture(fixture));

        var first = await sut.ReadAsync(context, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 23), null, CancellationToken.None);
        first.IsSuccess.ShouldBeTrue();
        first.Value.Items.Count.ShouldBe(1);
        first.Value.NextCursor.ShouldBe("1");
        var second = await sut.ReadAsync(context, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 23), first.Value.NextCursor, CancellationToken.None);
        second.IsSuccess.ShouldBeTrue();
        second.Value.Items.Count.ShouldBe(1);
        second.Value.Items[0].IsCancelled.ShouldBeTrue();
        (await sut.ReadAsync(context, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 23), "bad", CancellationToken.None))
            .Error!.Code.ShouldBe(CalendarSyncErrors.InvalidRemote.Code);
    }

    [Fact]
    public async Task State_is_isolated_by_organization_user_and_provider()
    {
        var sut = Enabled();
        var context = Context(CalendarProvider.Google);
        sut.Seed(context, ReadFixture("calendar-sync-google.json"));
        sut.Snapshot(Context(CalendarProvider.Microsoft365)).ShouldBeEmpty();
        sut.Snapshot(context with { UserId = Guid.NewGuid() }).ShouldBeEmpty();
        sut.Snapshot(context with { OrganizationId = Guid.NewGuid() }).ShouldBeEmpty();
        sut.Snapshot(context with { Provider = CalendarProvider.Microsoft365 }).ShouldBeEmpty();
        (await sut.ReadAsync(context, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21), null, CancellationToken.None))
            .Value.Items.Select(x => x.Id).ShouldBe(GoogleWindowEventIds);
    }

    [Fact]
    public async Task Create_is_idempotent_and_update_preserves_remote_text()
    {
        var sut = Enabled();
        var context = Context(CalendarProvider.Google);
        var original = ReadFixture("calendar-sync-google.json")[0];
        sut.Seed(context, [original]);
        var write = Write(original.ClientKey!);
        var existing = await sut.WriteAsync(context, write, null, null, CancellationToken.None);
        existing.IsSuccess.ShouldBeTrue();
        existing.Value.Id.ShouldBe(original.Id);
        existing.Value.Version.ShouldBe(original.Version);

        var updated = await sut.WriteAsync(context, write with { StartsAt = write.StartsAt.AddHours(1), EndsAt = write.EndsAt.AddHours(1) }, original.Id,
            original.Version, CancellationToken.None);
        updated.IsSuccess.ShouldBeTrue();
        updated.Value.Version.ShouldBe("8");
        updated.Value.Title.ShouldBe(original.Title);
        updated.Value.Description.ShouldBe(original.Description);
        updated.Value.Location.ShouldBe(original.Location);
        updated.Value.StartsAt.ShouldBe(write.StartsAt.AddHours(1));
        updated.Value.EndsAt.ShouldBe(write.EndsAt.AddHours(1));
    }

    [Fact]
    public async Task Simultaneous_conditional_writes_have_one_winner()
    {
        var sut = Enabled();
        var context = Context(CalendarProvider.Microsoft365);
        var item = ReadFixture("calendar-sync-microsoft365.json")[0];
        sut.Seed(context, [item]);
        var writes = await Task.WhenAll(
            sut.WriteAsync(context, Write("first"), item.Id, item.Version, CancellationToken.None),
            sut.WriteAsync(context, Write("second"), item.Id, item.Version, CancellationToken.None));
        writes.Count(result => result.IsSuccess).ShouldBe(1);
        writes.Count(result => result.IsFailure && result.Error.Code == CalendarSyncErrors.RemoteConflict.Code).ShouldBe(1);
    }

    [Fact]
    public async Task Unknown_remote_id_and_bad_context_are_rejected()
    {
        var sut = Enabled();
        var missing = await sut.WriteAsync(Context(CalendarProvider.Google), Write("new"), "known-elsewhere", null, CancellationToken.None);
        missing.Error!.Code.ShouldBe(CalendarSyncErrors.NotFound.Code);
        var invalid = await sut.ReadAsync(Context(CalendarProvider.Google) with { UserId = Guid.Empty },
            new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21), null, CancellationToken.None);
        invalid.Error!.Code.ShouldBe(CalendarSyncErrors.InvalidConnection.Code);
        var invalidProvider = await sut.ReadAsync(Context((CalendarProvider)99), new DateOnly(2026, 10, 20),
            new DateOnly(2026, 10, 21), null, CancellationToken.None);
        invalidProvider.Error!.Code.ShouldBe(CalendarSyncErrors.InvalidConnection.Code);
        var conditionalMissing = await sut.WriteAsync(Context(CalendarProvider.Google), Write("new"), "known-elsewhere", "1", CancellationToken.None);
        conditionalMissing.Error!.Code.ShouldBe(CalendarSyncErrors.RemoteConflict.Code);
    }

    [Fact]
    public async Task Get_reads_known_item_outside_window_and_keeps_scope_isolated()
    {
        var sut = Enabled();
        var context = Context(CalendarProvider.Google);
        var item = ReadFixture("calendar-sync-google.json")[0];
        sut.Seed(context, [item]);

        var found = await sut.GetAsync(context, item.Id, CancellationToken.None);
        found.IsSuccess.ShouldBeTrue();
        found.Value.ShouldBe(item);
        (await sut.GetAsync(context with { UserId = Guid.NewGuid() }, item.Id, CancellationToken.None)).Value.ShouldBeNull();
        (await sut.GetAsync(context, "missing", CancellationToken.None)).Value.ShouldBeNull();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Disabled_or_unavailable_provider_fails_reads_and_writes(bool enabled, bool available)
    {
        var sut = new FakeCalendarSyncProvider(new FakeCalendarSyncOptions { Enabled = enabled, Available = available });
        sut.IsSimulation.ShouldBeTrue();
        sut.IsEnabled.ShouldBe(enabled);
        var context = Context(CalendarProvider.Google);
        (await sut.ReadAsync(context, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21), null, CancellationToken.None))
            .Error!.Code.ShouldBe(CalendarSyncErrors.ProviderUnavailable.Code);
        (await sut.WriteAsync(context, Write("id"), null, null, CancellationToken.None))
            .Error!.Code.ShouldBe(CalendarSyncErrors.ProviderUnavailable.Code);
        (await sut.GetAsync(context, "remote-id", CancellationToken.None))
            .Error!.Code.ShouldBe(CalendarSyncErrors.ProviderUnavailable.Code);
    }

    private static FakeCalendarSyncProvider Enabled(int pageSize = 100) =>
        new(new FakeCalendarSyncOptions { Enabled = true, PageSize = pageSize });

    private static CalendarSyncContext Context(CalendarProvider provider) => new(OrganizationId, UserId, provider);

    private static CalendarSyncWrite Write(string clientKey) => new(clientKey, CalendarItemKind.Event,
        new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 20, 13, 0, 0, TimeSpan.Zero),
        null, false, "https://lexflow.test/item");

    private static IReadOnlyList<RemoteCalendarEvent> ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return JsonSerializer.Deserialize<IReadOnlyList<RemoteCalendarEvent>>(File.ReadAllText(path), JsonOptions)!;
    }
}

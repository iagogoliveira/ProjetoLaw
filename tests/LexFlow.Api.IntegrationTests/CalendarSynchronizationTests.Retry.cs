using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

public sealed partial class CalendarSynchronizationTests
{
    [Fact]
    public async Task Retrying_deadline_export_after_external_success_and_lost_local_link_restores_external_dates_immediately()
    {
        var owner = await TestOrganizations.CreateWithMemberAsync(_dependencies, CasePermissions);
        var connection = await ConnectAsync(owner);
        await DeadlineAsync(owner, await CaseAsync(owner));
        await SyncAsync(owner, connection);
        var original = _provider.Snapshot(Context(owner)).Single();
        using (var ambient = AmbientContext.Begin(owner.OrganizationId, "calendar-retry-test"))
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(owner.OrganizationId);
            var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
            var link = await db.CalendarSyncLinks.SingleAsync(l => l.ConnectionId == connection, TestContext.Current.CancellationToken);
            db.CalendarSyncLinks.Remove(link);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        _provider.Seed(Context(owner), [original with { Version = "2", Day = original.Day!.Value.AddDays(4), StartsAt = original.StartsAt.AddDays(4), EndsAt = original.EndsAt.AddDays(4) }]);
        await SyncAsync(owner, connection);
        var restored = _provider.Snapshot(Context(owner)).Single();
        restored.Id.ShouldBe(original.Id);
        restored.Day.ShouldBe(original.Day);
        restored.StartsAt.ShouldBe(original.StartsAt);
        restored.EndsAt.ShouldBe(original.EndsAt);
        (await PendingAsync(owner, connection)).GetArrayLength().ShouldBe(0);
    }
}

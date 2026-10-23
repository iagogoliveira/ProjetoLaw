using System.Text.Json;
using LexFlow.Api.IntegrationTests;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Scheduling.Application.CalendarSync;
using LexFlow.Scheduling.Domain.CalendarSync;
using LexFlow.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.SecurityTests;

public sealed partial class EndpointSecurityTests
{
    private async Task<Guid> CalendarConnectionOfAsync(SeededMember owner)
    {
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "calendar-security");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(owner.OrganizationId);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var connection = await db.CalendarConnections.SingleOrDefaultAsync(c => c.UserId == owner.UserId && c.Provider == CalendarProvider.Google, Ct);
        if (connection is not null) { return connection.Id; }
        connection = CalendarConnection.Connect(owner.UserId, CalendarProvider.Google, new DateOnly(2027, 4, 1), new DateOnly(2027, 5, 1)).Value;
        db.CalendarConnections.Add(connection);
        await db.SaveChangesAsync(Ct);
        return connection.Id;
    }

    private async Task<CrossTenantCase> CalendarPendingCaseAsync(SeededMember owner)
    {
        var connectionId = await CalendarConnectionOfAsync(owner);
        var localId = await EventOfAsync(owner);
        using var ambient = AmbientContext.Begin(owner.OrganizationId, "calendar-security");
        await using var scope = _factory.Services.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForNewOrganization(owner.OrganizationId);
        var db = scope.ServiceProvider.GetRequiredService<SchedulingDbContext>();
        var remote = new RemoteCalendarEvent(Guid.CreateVersion7().ToString("N"), "1", "Compromisso externo", null, null,
            new DateTimeOffset(2027, 4, 5, 13, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 4, 5, 14, 0, 0, TimeSpan.Zero), null, true, false, null);
        var link = CalendarSyncLink.Create(connectionId, CalendarItemKind.Event, localId, null, remote.Id);
        link.Pend(CalendarPendingReason.ExternalDeletion, JsonSerializer.Serialize(remote), "baseline");
        db.CalendarSyncLinks.Add(link);
        await db.SaveChangesAsync(Ct);
        return new CrossTenantCase(connectionId, new { resolution = "keepLocal" }, $"/api/v1/me/calendar-connections/{connectionId:D}/pending/{link.Id:D}/resolve");
    }
}

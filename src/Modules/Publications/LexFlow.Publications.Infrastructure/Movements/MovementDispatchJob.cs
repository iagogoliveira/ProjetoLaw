using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Publications.Application;
using LexFlow.Publications.Application.Movements;
using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Publications.Infrastructure.Movements;

public sealed class MovementDispatchJob(IServiceScopeFactory scopes)
{
    public async Task RunAsync()
    {
        await using var directoryScope = scopes.CreateAsyncScope();
        var directory = directoryScope.ServiceProvider.GetRequiredService<IOrganizationAccessDirectory>();
        foreach (var office in await directory.OfficeIdsAsync(CancellationToken.None))
        {
            using var ambient = AmbientContext.Begin(office, "movement-dispatch");
            await using var scope = scopes.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office);
            await DispatchOfficeAsync(scope.ServiceProvider);
        }
    }
    private static async Task DispatchOfficeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<IPublicationsDbContext>();
        var configuration = await db.MovementIntegrations.AsNoTracking().SingleOrDefaultAsync();
        if (configuration is null || configuration.Provider == MovementProviderKind.Disabled) { return; }
        var cases = services.GetRequiredService<ICaseDirectory>();
        var clock = services.GetRequiredService<IClock>();
        var jobs = services.GetRequiredService<IJobScheduler>();
        Guid? after = null;
        while (true)
        {
            var page = await cases.MonitoredAsync(after, 200, CancellationToken.None);
            if (page.Count == 0) { return; }
            var ids = page.Select(c => c.Id).ToArray();
            var states = await db.MovementSyncs.AsNoTracking().Where(s => ids.Contains(s.LegalCaseId)).ToDictionaryAsync(s => s.LegalCaseId);
            foreach (var item in page)
            {
                states.TryGetValue(item.Id, out var state);
                if (MovementSchedule.IsDue(state, configuration.Revision, item.Id, clock.UtcNow)) { jobs.Enqueue<MovementSyncJob>(job => job.RunAsync(item.Id)); }
            }
            after = page[^1].Id;
        }
    }
}

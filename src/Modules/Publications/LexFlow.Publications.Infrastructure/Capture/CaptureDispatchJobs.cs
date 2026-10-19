using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Publications.Application;
using LexFlow.Publications.Application.Capture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Publications.Infrastructure.Capture;

/// <summary>Dispatchers independentes: uma falha no provedor não impede o monitor da captura.</summary>
public sealed class CaptureDispatchJobs(IServiceScopeFactory scopes)
{
    public Task CaptureAsync() => DispatchAsync(monitor: false);
    public Task MonitorAsync() => DispatchAsync(monitor: true);

    private async Task DispatchAsync(bool monitor)
    {
        await using var directoryScope = scopes.CreateAsyncScope();
        var directory = directoryScope.ServiceProvider.GetRequiredService<IOrganizationAccessDirectory>();
        foreach (var office in await directory.OfficeIdsAsync(CancellationToken.None))
        {
            using var ambient = AmbientContext.Begin(office, "capture-dispatch");
            await using var scope = scopes.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(office);
            var db = scope.ServiceProvider.GetRequiredService<IPublicationsDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var jobs = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
            var sources = await db.Sources.AsNoTracking().Where(s => s.IsActive).ToListAsync();
            foreach (var source in sources)
            {
                if (monitor)
                {
                    var activatedDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(CaptureSchedule.ActiveSince(source), clock.LegalTimeZone).DateTime);
                    foreach (var day in new[] { clock.Today.AddDays(-1), clock.Today })
                    {
                        if (day >= activatedDay && clock.UtcNow >= CaptureSchedule.Deadline(source, day, clock.LegalTimeZone))
                        {
                            jobs.Enqueue<CaptureMonitor>(job => job.RunAsync(source.Id, day));
                        }
                    }
                }
                else
                {
                    foreach (var day in new[] { clock.Today.AddDays(-1), clock.Today })
                    {
                        if (CaptureSchedule.CanCapture(source, day, clock.UtcNow, clock.LegalTimeZone, force: false))
                        {
                            jobs.Enqueue<CaptureSourceJob>(job => job.RunAsync(source.Id, day, false));
                        }
                    }
                }
            }
        }
    }
}

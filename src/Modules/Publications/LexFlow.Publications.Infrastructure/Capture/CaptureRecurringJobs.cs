using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Publications.Infrastructure.Movements;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LexFlow.Publications.Infrastructure.Capture;

internal sealed class CaptureRecurringJobs(IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
        jobs.AddOrUpdateRecurring<CaptureDispatchJobs>("publications-capture", job => job.CaptureAsync(), "*/5 * * * *");
        jobs.AddOrUpdateRecurring<CaptureDispatchJobs>("publications-capture-monitor", job => job.MonitorAsync(), "*/5 * * * *");
        jobs.AddOrUpdateRecurring<MovementDispatchJob>("publications-movements", job => job.RunAsync(), "*/5 * * * *");
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

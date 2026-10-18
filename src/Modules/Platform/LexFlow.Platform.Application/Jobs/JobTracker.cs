using LexFlow.Application.Abstractions;
using LexFlow.Platform.Application.Abstractions;
using LexFlow.Platform.Contracts.Jobs;
using LexFlow.Platform.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Application.Jobs;

/// <summary>Grava os estados do job no <c>platform.jobs</c> do escritório ativo (o job de outro escritório não aparece).</summary>
internal sealed class JobTracker(IPlatformDbContext db, IIdGenerator ids, IClock clock) : IJobTracker
{
    public async Task<Guid> CreateAsync(string type, CancellationToken cancellationToken)
    {
        var job = Job.Enqueue(ids.NewId(), type);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public Task StartAsync(Guid jobId, CancellationToken cancellationToken) => ChangeAsync(jobId, job => job.Start(clock.UtcNow), cancellationToken);

    public Task SucceedAsync(Guid jobId, string? resultLocation, CancellationToken cancellationToken) =>
        ChangeAsync(jobId, job => job.Succeed(clock.UtcNow, resultLocation), cancellationToken);

    public Task FailAsync(Guid jobId, string errorCode, CancellationToken cancellationToken) =>
        ChangeAsync(jobId, job => job.Fail(clock.UtcNow, errorCode), cancellationToken);

    /// <summary>Transição fora de ordem (ex.: a tentativa repetida do Hangfire) é ignorada: o estado já registrado vale.</summary>
    private async Task ChangeAsync(Guid jobId, Func<Job, SharedKernel.Results.Result> change, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is not null && change(job).IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

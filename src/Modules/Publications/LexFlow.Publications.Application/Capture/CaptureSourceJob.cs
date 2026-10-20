using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Capture;

public sealed class CaptureSourceJob(IPublicationsDbContext db, ICaptureLocks locks, IPublicationProvider provider,
    ICommandHandler<ReceivePublicationCommand, Guid> intake, IClock clock)
{
    public Task RunAsync(Guid sourceId, DateOnly day, bool force = false) => RunCoreAsync(sourceId, day, force, CancellationToken.None);

    private async Task RunCoreAsync(Guid sourceId, DateOnly day, bool force, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        if (!await locks.TrySourceAsync(sourceId, day, cancellationToken)) { return; }
        var snapshot = await db.Sources.AsNoTracking().Where(s => s.Id == sourceId).Select(s => new
        {
            Source = s,
            PeriodId = db.SourcePeriods.Where(p => p.SourceId == s.Id && p.DeactivatedAt == null)
                .OrderByDescending(p => p.ActivatedAt).ThenByDescending(p => p.Id).Select(p => (Guid?)p.Id).FirstOrDefault(),
        }).SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null || !CaptureSchedule.CanCapture(snapshot.Source, day, clock.UtcNow, clock.LegalTimeZone, force)) { return; }
        var source = snapshot.Source;
        var activation = CaptureSchedule.ActiveSince(source);
        var previous = await db.Runs.AsNoTracking().Where(r => r.SourceId == sourceId && r.Day == day
            && (r.SourcePeriodId != null ? r.SourcePeriodId == snapshot.PeriodId : r.SourceActivatedAt == activation))
            .OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(cancellationToken);
        if (!force && previous is not null && (previous.Status == CaptureRunStatus.Succeeded
            || previous.StartedAt > clock.UtcNow.Subtract(CaptureSchedule.RetryInterval))) { return; }

        var run = CaptureRun.Start(sourceId, day, clock.UtcNow, activation, snapshot.PeriodId).Value;
        db.Runs.Add(run);
        var result = await FetchAsync(source, day, cancellationToken);
        await locks.ControlAsync(cancellationToken);
        var (count, error) = result.IsFailure ? (0, result.Error.Code) : await StoreAsync(sourceId, result.Value, cancellationToken);
        run.Finish(count, error, clock.UtcNow);
        if (error is null)
        {
            foreach (var alert in await db.Alerts.Where(a => a.SourceId == sourceId && a.Day == day && a.ResolvedAt == null).ToListAsync(cancellationToken))
            {
                alert.Resolve(clock.UtcNow);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Result<IReadOnlyList<CapturedPublication>>> FetchAsync(CaptureSource source, DateOnly day, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            return await provider.FetchAsync(new(source.Id, source.Kind.ToString(), source.Value, day), timeout.Token)
                .WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return Error.BusinessRule("publications.provider_unavailable", "Provedor de captura indisponível.");
        }
    }

    private async Task<(int Count, string? Error)> StoreAsync(Guid sourceId, IReadOnlyList<CapturedPublication> items, CancellationToken cancellationToken)
    {
        // Ordem global dos locks por hash evita deadlock entre fontes com os mesmos textos.
        var inputs = items.Select(p => new PublicationInput(sourceId, p.Journal, p.AvailableOn, p.PublishedOn, p.Text))
            .Select(p => (Input: p, Validated: Publication.Receive(p, clock.UtcNow))).ToList();
        var invalid = inputs.FirstOrDefault(p => p.Validated.IsFailure).Validated;
        if (invalid is { IsFailure: true }) { return (0, invalid.Error.Code); }
        var count = 0;
        foreach (var item in inputs.OrderBy(p => p.Validated.Value.Hash, StringComparer.Ordinal))
        {
            var received = await intake.Handle(new ReceivePublicationCommand(item.Input), cancellationToken);
            if (received.IsFailure) { return (count, received.Error.Code); }
            count++;
        }
        return (count, null);
    }
}

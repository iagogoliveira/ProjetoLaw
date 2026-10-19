using LexFlow.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Contracts;
using LexFlow.Publications.Domain;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Movements;

public sealed class MovementSyncJob(IPublicationsDbContext db, ICaptureLocks locks, ICaseDirectory cases, IConfiguredMovementProvider provider, IClock clock)
{
    public async Task RunAsync(Guid caseId)
    {
        var ct = CancellationToken.None;
        await using var transaction = await db.BeginTransactionAsync(ct);
        if (!await locks.TryMovementAsync(caseId, ct)) { return; }
        var configuration = await db.MovementIntegrations.AsNoTracking().SingleOrDefaultAsync(ct);
        if (configuration is null || configuration.Provider == MovementProviderKind.Disabled) { return; }
        var monitored = await cases.MonitoredCaseAsync(caseId, ct);
        if (monitored is null) { return; }
        var state = await db.MovementSyncs.SingleOrDefaultAsync(s => s.LegalCaseId == caseId, ct);
        if (!MovementSchedule.IsDue(state, configuration.Revision, caseId, clock.UtcNow)) { return; }
        if (state is null) { state = MovementSync.Create(caseId); db.MovementSyncs.Add(state); }
        state.ResetFor(configuration.Revision);
        state.Attempt(clock.UtcNow);
        var result = await FetchAsync(configuration, new MovementQuery(monitored.Cnj, state.Cursor));
        var error = result.IsFailure ? result.Error : await StoreAsync(caseId, configuration.Provider, state, result.Value, ct);
        if (error is not null)
        {
            if (string.Equals(error.Code, "publications.datajud_cursor_invalid", StringComparison.Ordinal)) { state.Restart(); }
            state.Fail(error.Code);
        }
        else { state.AcceptPage(result.Value.Tribunal, result.Value.NextCursor, MovementSchedule.Slot(caseId, clock.UtcNow), clock.UtcNow); }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Error?> StoreAsync(Guid caseId, MovementProviderKind kind, MovementSync state, MovementPage page, CancellationToken ct)
    {
        if (page.NextCursor is not null && (page.NextCursor.Length > 4096 || string.Equals(state.Cursor, page.NextCursor, StringComparison.Ordinal) || state.PageCount >= 1000))
        { return Error.BusinessRule("publications.datajud_cursor_invalid", "Cursor DataJud não avançou ou excedeu o limite."); }
        var movements = page.Items.Select(m => CaseMovement.Import(caseId, new MovementInput(m.OccurredAt, m.Description), clock.UtcNow, m.ExternalId,
            kind == MovementProviderKind.Fake ? MovementOrigin.Provider : MovementOrigin.DataJud)).ToList();
        var failure = movements.FirstOrDefault(m => m.IsFailure);
        if (failure is not null) { return failure.Error; }
        var ids = movements.Select(m => m.Value.ExternalId).ToArray();
        var origin = kind == MovementProviderKind.Fake ? MovementOrigin.Provider : MovementOrigin.DataJud;
        var existing = (await db.Movements.AsNoTracking().Where(m => m.LegalCaseId == caseId && m.Origin == origin && ids.Contains(m.ExternalId))
            .Select(m => m.ExternalId).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        foreach (var movement in movements.Select(m => m.Value))
        {
            if (existing.Add(movement.ExternalId)) { db.Movements.Add(movement); }
        }
        return null;
    }
    private async Task<Result<MovementPage>> FetchAsync(MovementIntegration configuration, MovementQuery query)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { return await provider.FetchAsync(configuration, query, timeout.Token).WaitAsync(TimeSpan.FromSeconds(30)); }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or OperationCanceledException)
        { return Error.BusinessRule("publications.provider_unavailable", "Provedor de movimentações indisponível."); }
    }
}

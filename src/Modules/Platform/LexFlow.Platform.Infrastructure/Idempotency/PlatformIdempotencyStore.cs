using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Idempotency;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Platform.Domain.Idempotency;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LexFlow.Platform.Infrastructure.Idempotency;

/// <summary>
/// <see cref="IIdempotencyStore"/> sobre <c>platform.idempotency_keys</c>. Cada operação usa um DbContext próprio
/// (escopo novo, mesmo tenant da requisição) para não gravar junto as alterações pendentes do handler.
/// A unicidade (<c>organization_id</c>, <c>key</c>) resolve requisições simultâneas com a mesma chave.
/// </summary>
internal sealed class PlatformIdempotencyStore(IServiceScopeFactory scopes, IClock clock, IIdGenerator ids) : IIdempotencyStore
{
    public async Task<IdempotencyBegin> BeginAsync(string key, string requestHash, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var existing = await db.IdempotencyKeys.SingleOrDefaultAsync(k => k.Key == key, cancellationToken);
        if (existing is not null && existing.ExpiresAt <= clock.UtcNow)
        {
            db.IdempotencyKeys.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
            existing = null;
        }

        if (existing is not null)
        {
            return Classify(existing, requestHash);
        }

        db.IdempotencyKeys.Add(IdempotencyKey.Begin(ids.NewId(), key, requestHash, clock.UtcNow));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new IdempotencyBegin(IdempotencyOutcome.Started);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Outra requisição com a mesma chave gravou primeiro.
            await using var retryScope = scopes.CreateAsyncScope();
            var retry = retryScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var winner = await retry.IdempotencyKeys.AsNoTracking().SingleAsync(k => k.Key == key, cancellationToken);
            return Classify(winner, requestHash);
        }
    }

    public async Task CompleteAsync(string key, StoredResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entry = await db.IdempotencyKeys.SingleAsync(k => k.Key == key, cancellationToken);
        entry.Complete(response.StatusCode, response.ContentType, response.Body);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AbandonAsync(string key, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await db.IdempotencyKeys.Where(k => k.Key == key && k.ResponseStatusCode == null).ExecuteDeleteAsync(cancellationToken);
    }

    private static IdempotencyBegin Classify(IdempotencyKey entry, string requestHash)
    {
        if (!string.Equals(entry.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return new IdempotencyBegin(IdempotencyOutcome.KeyReused);
        }

        return entry.IsCompleted
            ? new IdempotencyBegin(IdempotencyOutcome.Replay, new StoredResponse(entry.ResponseStatusCode!.Value, entry.ResponseContentType, entry.ResponseBody))
            : new IdempotencyBegin(IdempotencyOutcome.InProgress);
    }
}

/// <summary>Job diário (Worker) que remove chaves expiradas de todas as organizações.</summary>
public sealed class IdempotencyKeysCleanupJob(PlatformDbContext db, IClock clock)
{
    public const string RecurringJobId = "platform-idempotency-keys-cleanup";

    /// <summary>Todo dia às 03:00 (America/Sao_Paulo).</summary>
    public const string Cron = "0 3 * * *";

    /// <summary>
    /// Operação de plataforma, entre organizações. Com a RLS, a aplicação não enxerga outros tenants: a exclusão roda
    /// numa função do dono do banco (<c>platform.delete_expired_idempotency_keys</c>, ADR 0019).
    /// </summary>
    public Task<int> RunAsync()
    {
        var now = clock.UtcNow;
        return db.Database.SqlQuery<int>($"SELECT platform.delete_expired_idempotency_keys({now}) AS \"Value\"").SingleAsync();
    }
}

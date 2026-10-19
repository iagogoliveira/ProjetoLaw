using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using LexFlow.Publications.Application.Capture;
using LexFlow.Publications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Infrastructure.Capture;

internal sealed class PostgresCaptureLocks(PublicationsDbContext db) : ICaptureLocks
{
    public async Task ControlAsync(CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var key = Key("publications-control");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }
    public async Task IntegrationAsync(CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var key = Key("movement-integration");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }
    public async Task<bool> TryMovementAsync(Guid caseId, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var key = Key("movement:" + caseId.ToString("N"));
        return await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({key}) AS \"Value\"").SingleAsync(cancellationToken);
    }
    public async Task<bool> TrySourceAsync(Guid sourceId, DateOnly day, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var key = Key("source:" + sourceId.ToString("N") + ":" + day.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture));
        return await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({key}) AS \"Value\"").SingleAsync(cancellationToken);
    }
    public async Task PublicationAsync(string hash, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var key = Key("publication:" + hash);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }
    private long Key(string value) => BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(
        Encoding.UTF8.GetBytes(db.CurrentOrganizationId!.Value.ToString("N") + ":" + value)));
    private void EnsureTransaction()
    {
        if (!db.HasActiveTransaction || db.CurrentOrganizationId is null)
        {
            throw new InvalidOperationException("Lock de captura exige transação e tenant ativos.");
        }
    }
}

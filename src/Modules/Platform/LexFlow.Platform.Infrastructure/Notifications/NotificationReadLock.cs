using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using LexFlow.Platform.Application.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Platform.Infrastructure.Notifications;

internal sealed class NotificationReadLock(PlatformDbContext db) : INotificationReadLock
{
    public async Task AcquireAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken)
    {
        if (!db.HasActiveTransaction || db.CurrentOrganizationId is null) { throw new InvalidOperationException("Leitura exige transação e tenant."); }
        var text = db.CurrentOrganizationId.Value.ToString("N") + notificationId.ToString("N") + userId.ToString("N");
        var key = BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
    }
}

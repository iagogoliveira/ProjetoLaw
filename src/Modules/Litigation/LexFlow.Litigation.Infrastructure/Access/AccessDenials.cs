using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence.Auditing;
using LexFlow.Litigation.Application.Access;
using LexFlow.Litigation.Infrastructure.Persistence;

namespace LexFlow.Litigation.Infrastructure.Access;

/// <summary>
/// <c>AccessDenied</c> em <c>audit.audit_logs</c> (seção 6.3): gravado na hora, fora da transação do caso de uso, que
/// nem chega a abrir (o autorizador roda antes). A linha entra no encadeamento de hashes como as outras.
/// </summary>
internal sealed class AccessDenials(
    LitigationDbContext db,
    IClock clock,
    IIdGenerator ids,
    ICurrentUser user,
    ITenantContext tenant,
    IRequestContext request) : IAccessDenials
{
    public async Task RecordAsync(string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        db.Set<AuditLogEntry>().Add(new AuditLogEntry
        {
            Id = ids.NewId(),
            OrganizationId = tenant.OrganizationId,
            UserId = user.UserId,
            Action = AuditActions.AccessDenied,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Changes = "{}",
            Ip = request.IpAddress,
            UserAgent = request.UserAgent,
            CorrelationId = request.CorrelationId,
            OccurredAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

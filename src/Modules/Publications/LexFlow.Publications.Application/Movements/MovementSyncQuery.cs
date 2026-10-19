using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Litigation.Contracts;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Movements;

public sealed record MovementSyncResponse(Guid CaseId, string? Tribunal, DateTimeOffset? LastAttemptAt, DateTimeOffset? LastSuccessAt, bool HasPendingPage, string? ErrorCode);
public sealed record GetMovementSyncQuery(Guid CaseId) : IQuery<MovementSyncResponse>;
internal sealed class MovementSyncAuthorizer(ICaseDirectory cases) : IResourceAuthorizer<GetMovementSyncQuery>
{
    public Task<Result> AuthorizeAsync(GetMovementSyncQuery request, CancellationToken cancellationToken) => cases.AuthorizeAsync(request.CaseId, "cases.read", cancellationToken);
}
internal sealed class GetMovementSyncHandler(IPublicationsDbContext db) : IQueryHandler<GetMovementSyncQuery, MovementSyncResponse>
{
    public async Task<Result<MovementSyncResponse>> Handle(GetMovementSyncQuery query, CancellationToken cancellationToken)
    {
        var state = await db.MovementSyncs.AsNoTracking().SingleOrDefaultAsync(s => s.LegalCaseId == query.CaseId, cancellationToken);
        return new MovementSyncResponse(query.CaseId, state?.Tribunal, state?.LastAttemptAt, state?.LastSuccessAt, state?.Cursor is not null, state?.ErrorCode);
    }
}

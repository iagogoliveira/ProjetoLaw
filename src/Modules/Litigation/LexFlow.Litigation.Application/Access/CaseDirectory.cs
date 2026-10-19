using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Access;

internal sealed class CaseDirectory(ILitigationDbContext db, CaseAccess access) : ICaseDirectory
{
    public async Task<IReadOnlyCollection<string>> InaccessibleCnjsAsync(string permission, CancellationToken cancellationToken)
    {
        var context = await access.ContextAsync(permission, cancellationToken);
        var visible = CaseAccess.Visible(db.Cases.AsNoTracking(), context).Select(c => c.Id);
        return await db.Cases.AsNoTracking().Where(c => c.CnjNumber != null && !visible.Contains(c.Id))
            .Select(c => c.CnjNumber!).ToListAsync(cancellationToken);
    }

    public Task<Result> AuthorizeAsync(Guid caseId, string permission, CancellationToken cancellationToken) =>
        access.AuthorizeAsync(caseId, permission, cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> VisibleIdsAsync(string permission, CancellationToken cancellationToken)
    {
        var context = await access.ContextAsync(permission, cancellationToken);
        return await CaseAccess.Visible(db.Cases.AsNoTracking(), context).Select(c => c.Id).ToListAsync(cancellationToken);
    }

    public async Task<Guid?> FindByCnjAsync(string cnj, CancellationToken cancellationToken)
    {
        var matches = await db.Cases.AsNoTracking().Where(c => c.CnjNumber == cnj).Select(c => c.Id).Take(2).ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }
}

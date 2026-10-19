using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Contracts;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Access;

internal sealed class CaseDirectory(ILitigationDbContext db, CaseAccess access) : ICaseDirectory
{
    public async Task<IReadOnlyList<MonitoredCase>> MonitoredAsync(Guid? after, int limit, CancellationToken cancellationToken) =>
        await db.Cases.AsNoTracking().Where(c => c.Kind == CaseKind.Judicial && c.Status == CaseStatus.Active && c.CnjNumber != null)
            .Where(c => after == null || c.Id.CompareTo(after.Value) > 0).OrderBy(c => c.Id).Take(Math.Clamp(limit, 1, 200))
            .Select(c => new MonitoredCase(c.Id, c.CnjNumber!)).ToListAsync(cancellationToken);

    public Task<MonitoredCase?> MonitoredCaseAsync(Guid id, CancellationToken cancellationToken) =>
        db.Cases.AsNoTracking().Where(c => c.Id == id && c.Kind == CaseKind.Judicial && c.Status == CaseStatus.Active && c.CnjNumber != null)
            .Select(c => new MonitoredCase(c.Id, c.CnjNumber!)).SingleOrDefaultAsync(cancellationToken);

    public async Task<CaseReadScope> ScopeForUserAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        var context = await access.ContextForUserAsync(userId, permission, cancellationToken);
        var visible = CaseAccess.Visible(db.Cases.AsNoTracking(), context).Select(c => c.Id);
        var ids = await visible.ToListAsync(cancellationToken);
        var cnjs = await db.Cases.AsNoTracking().Where(c => c.CnjNumber != null && !visible.Contains(c.Id))
            .Select(c => c.CnjNumber!).ToListAsync(cancellationToken);
        return new CaseReadScope(ids, cnjs);
    }

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

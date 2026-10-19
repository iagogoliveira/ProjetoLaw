using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Publications.Application.Capture;

internal sealed class PublicationNotificationVisibility(IPublicationsDbContext db, ICaseDirectory cases, IOrganizationAccessDirectory directory) : INotificationVisibility
{
    public async Task<IReadOnlyCollection<Guid>> VisibleAsync(NotificationViewer viewer, IReadOnlyList<NotificationCandidate> candidates, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var access = await directory.GetAsync(viewer.UserId, cancellationToken);
        if (access is null || (access.MfaRequired && !viewer.MultiFactorAuthenticated)) { return []; }
        var visible = new List<Guid>();
        if (HasOfficeAlertAccess(access))
        {
            var alertIds = candidates.Where(c => string.Equals(c.Kind, "publications.capture_missing", StringComparison.Ordinal)).Select(c => c.ResourceId).ToList();
            var valid = await db.Alerts.Where(a => alertIds.Contains(a.Id)).Select(a => a.Id).ToListAsync(cancellationToken);
            visible.AddRange(candidates.Where(c => string.Equals(c.Kind, "publications.capture_missing", StringComparison.Ordinal) && valid.Contains(c.ResourceId)).Select(c => c.Id));
        }
        if (!access.Permissions.ContainsKey("publications.read")) { return visible; }
        var scope = await cases.ScopeForUserAsync(viewer.UserId, "publications.read", cancellationToken);
        var unlinked = access.Permissions["publications.read"].Any(s => s.Scope == OfficeAccessScope.Organization);
        var ids = candidates.Where(c => string.Equals(c.Kind, "publications.received", StringComparison.Ordinal)).Select(c => c.ResourceId).ToList();
        var resources = await db.Publications.AsNoTracking().Where(p => ids.Contains(p.Id))
            .Where(p => !p.CandidateCnjs.Any(c => scope.HiddenCnjs.Contains(c)))
            .Where(p => p.LegalCaseId == null ? unlinked : scope.VisibleIds.Contains(p.LegalCaseId.Value))
            .Select(p => p.Id).ToListAsync(cancellationToken);
        visible.AddRange(candidates.Where(c => string.Equals(c.Kind, "publications.received", StringComparison.Ordinal) && resources.Contains(c.ResourceId)).Select(c => c.Id));
        return visible;
    }

    public static bool HasOfficeAlertAccess(OfficeUserAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);
        return Has("settings.integrations.manage") || Has("publications.triage");
        bool Has(string permission) => access.Permissions.TryGetValue(permission, out var scopes) && scopes.Any(s => s.Scope == OfficeAccessScope.Organization);
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Litigation.Contracts;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Scheduling.Application;
using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.UnitTests;

public sealed class DeadlineNotificationVisibilityTests
{
    private static readonly Guid CaseId = Guid.CreateVersion7();
    private static readonly Guid AssigneeId = Guid.CreateVersion7();
    private static readonly Guid ReviewerId = Guid.CreateVersion7();
    private static readonly Guid CoordinatorId = Guid.CreateVersion7();

    [Fact]
    public async Task Assignee_and_reviewer_can_see_alert_when_both_read_permissions_reach_the_case()
    {
        foreach (var userId in new[] { AssigneeId, ReviewerId })
        {
            var cases = new CaseDirectoryStub(CaseId, CaseId);
            var visibility = CreateVisibility(new OrganizationAccessStub(AccessWith("cases.read", "schedule.deadlines.read")), cases);

            var visible = await visibility.VisibleToAsync(new NotificationViewer(userId, false), Alert(ReviewerId), CancellationToken.None);

            visible.ShouldBeTrue();
            cases.RequestedPermissions.ShouldContain("cases.read");
            cases.RequestedPermissions.ShouldContain("schedule.deadlines.read");
        }
    }

    [Fact]
    public async Task Verifier_can_see_alert_only_when_verify_scope_and_both_read_scopes_reach_the_case()
    {
        var cases = new CaseDirectoryStub(CaseId, CaseId, CaseId);
        var visibility = CreateVisibility(new OrganizationAccessStub(AccessWith(
            "cases.read", "schedule.deadlines.read", "schedule.deadlines.verify")), cases);

        var visible = await visibility.VisibleToAsync(new NotificationViewer(CoordinatorId, false), Alert(), CancellationToken.None);

        visible.ShouldBeTrue();
        cases.RequestedPermissions.ShouldContain("schedule.deadlines.verify");
    }

    [Fact]
    public async Task Verifier_without_case_scope_cannot_see_alert()
    {
        var cases = new CaseDirectoryStub(CaseId, CaseId, Guid.CreateVersion7());
        var visibility = CreateVisibility(new OrganizationAccessStub(AccessWith(
            "cases.read", "schedule.deadlines.read", "schedule.deadlines.verify")), cases);

        var visible = await visibility.VisibleToAsync(new NotificationViewer(CoordinatorId, false), Alert(), CancellationToken.None);

        visible.ShouldBeFalse();
    }

    [Fact]
    public async Task Case_hidden_from_either_read_permission_cannot_be_seen_even_by_assignee()
    {
        var cases = new CaseDirectoryStub(CaseId, Guid.CreateVersion7());
        var visibility = CreateVisibility(new OrganizationAccessStub(AccessWith("cases.read", "schedule.deadlines.read")), cases);

        var visible = await visibility.VisibleToAsync(new NotificationViewer(AssigneeId, false), Alert(), CancellationToken.None);

        visible.ShouldBeFalse();
        cases.RequestedPermissions.ShouldContain("cases.read");
        cases.RequestedPermissions.ShouldContain("schedule.deadlines.read");
    }

    [Fact]
    public async Task Removed_member_cannot_see_alert()
    {
        var cases = new CaseDirectoryStub(CaseId, CaseId);
        var visibility = CreateVisibility(new OrganizationAccessStub(null), cases);

        var visible = await visibility.VisibleToAsync(new NotificationViewer(AssigneeId, false), Alert(), CancellationToken.None);

        visible.ShouldBeFalse();
        cases.RequestedPermissions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Mfa_requirement_is_respected(bool mfaRequired, bool authenticated)
    {
        var cases = new CaseDirectoryStub(CaseId, CaseId);
        var access = new OrganizationAccessStub(AccessWith("cases.read", "schedule.deadlines.read") with { MfaRequired = mfaRequired });
        var visibility = CreateVisibility(access, cases);

        var visible = await visibility.VisibleToAsync(new NotificationViewer(AssigneeId, authenticated), Alert(), CancellationToken.None);

        visible.ShouldBe(!mfaRequired || authenticated);
    }

    [Fact]
    public async Task Unknown_activity_kind_is_never_visible()
    {
        var cases = new CaseDirectoryStub(CaseId, CaseId);
        var visibility = CreateVisibility(new OrganizationAccessStub(AccessWith("cases.read", "schedule.deadlines.read")), cases);

        var visible = await visibility.VisibleToAsync(new NotificationViewer(AssigneeId, false), Alert() with { ActivityKind = "scheduling.deadline_completed" }, CancellationToken.None);

        visible.ShouldBeFalse();
        cases.RequestedPermissions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Assignee_without_deadline_read_permission_cannot_see_alert()
    {
        var cases = new CaseDirectoryStub(CaseId, CaseId);
        var visibility = CreateVisibility(new OrganizationAccessStub(AccessWith("cases.read")), cases);

        var visible = await visibility.VisibleToAsync(new NotificationViewer(AssigneeId, false), Alert(), CancellationToken.None);

        visible.ShouldBeFalse();
        cases.RequestedPermissions.ShouldBeEmpty();
    }

    private static DeadlineNotificationVisibility CreateVisibility(OrganizationAccessStub access, CaseDirectoryStub cases) =>
        new(new EmptySchedulingDbContext(), cases, access, new FixedClock());

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        public TimeZoneInfo LegalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        public DateOnly Today => new(2026, 10, 21);
    }

    private static OfficeUserAccess AccessWith(params string[] permissions) =>
        new(false, permissions.ToDictionary(permission => permission,
            _ => (IReadOnlyList<OfficeScope>)[new OfficeScope(OfficeAccessScope.Organization, null)], StringComparer.Ordinal));

    private static DeadlineAlertDetails Alert(Guid? reviewedBy = null) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), CaseId, "scheduling.deadline_missed", AssigneeId, reviewedBy);

    private sealed class OrganizationAccessStub(OfficeUserAccess? access) : IOrganizationAccessDirectory
    {
        public Task<OfficeUserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(access);
        public Task<IReadOnlyList<Guid>> UserIdsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> OfficeIdsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CaseDirectoryStub(params Guid[] visibleCases) : ICaseDirectory
    {
        public List<string> RequestedPermissions { get; } = [];

        public Task<CaseReadScope> ScopeForUserAsync(Guid userId, string permission, CancellationToken cancellationToken)
        {
            RequestedPermissions.Add(permission);
            var scope = permission switch
            {
                "cases.read" => visibleCases.ElementAtOrDefault(0),
                "schedule.deadlines.read" => visibleCases.ElementAtOrDefault(1),
                "schedule.deadlines.verify" => visibleCases.ElementAtOrDefault(2),
                _ => Guid.Empty,
            };
            IReadOnlyCollection<Guid> ids = scope == Guid.Empty ? [] : [scope];
            return Task.FromResult(new CaseReadScope(ids, []));
        }

        public Task<IReadOnlyList<PublicationCaseOption>> PublicationOptionsAsync(string? term, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> IsOpenAsync(Guid caseId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> AuthorizeAsync(Guid caseId, string permission, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> VisibleIdsAsync(string permission, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<string>> InaccessibleCnjsAsync(string permission, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid?> FindByCnjAsync(string cnj, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<MonitoredCase>> MonitoredAsync(Guid? after, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<MonitoredCase?> MonitoredCaseAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CaseLocation?> LocationAsync(Guid caseId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class EmptySchedulingDbContext : ISchedulingDbContext
    {
        public DbSet<Deadline> Deadlines => throw new NotSupportedException();
        public DbSet<TaskItem> Tasks => throw new NotSupportedException();
        public DbSet<CalendarEntry> CalendarEntries => throw new NotSupportedException();
        public DbSet<DeadlineActivity> DeadlineActivities => throw new NotSupportedException();
        public DbSet<CalendarEvent> Events => throw new NotSupportedException();
        public DbSet<TaskChecklistItem> TaskChecklistItems => throw new NotSupportedException();
        public DbSet<TaskComment> TaskComments => throw new NotSupportedException();
        public DbSet<LexFlow.Scheduling.Domain.CalendarSync.CalendarConnection> CalendarConnections => throw new NotSupportedException();
        public DbSet<LexFlow.Scheduling.Domain.CalendarSync.CalendarSyncLink> CalendarSyncLinks => throw new NotSupportedException();
        public DbSet<LexFlow.Scheduling.Domain.CalendarSync.SimulatedCalendarItem> SimulatedCalendarItems => throw new NotSupportedException();
        public bool HasActiveTransaction => false;
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LockDeadlinesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

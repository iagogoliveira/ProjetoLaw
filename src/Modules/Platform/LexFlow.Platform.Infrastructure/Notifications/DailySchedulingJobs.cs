using System.Globalization;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Contracts;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Domain.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Scheduling.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Platform.Infrastructure.Notifications;

public sealed class DailySchedulingDispatchJob(IServiceScopeFactory scopes)
{
    public const string RecurringJobId = "platform-daily-scheduling";
    public const string Cron = "30 7 * * *";

    public async Task RunAsync()
    {
        await using var scope = scopes.CreateAsyncScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var day = DailySchedulingTime.Day(clock);
        if (!DailySchedulingTime.Ready(clock, day)) { return; }
        var offices = await scope.ServiceProvider.GetRequiredService<IOrganizationAccessDirectory>().OfficeIdsAsync(CancellationToken.None);
        var jobs = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
        foreach (var office in offices) { jobs.Enqueue<DailyOfficeNotificationsJob>(job => job.RunAsync(office, day)); }
    }
}

public sealed class DailyOfficeNotificationsJob(IServiceScopeFactory scopes)
{
    public async Task RunAsync(Guid officeId, DateOnly day)
    {
        using var ambient = AmbientContext.Begin(officeId, "daily-scheduling");
        await using var scope = scopes.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(officeId);
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        if (!DailySchedulingTime.Ready(clock, day)) { return; }
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var deadlines = scope.ServiceProvider.GetRequiredService<IFatalDeadlineReminders>();
        await AddRemindersAsync(db, deadlines, day, clock.UtcNow);
        var users = await db.NotificationPreferences.AsNoTracking().Where(p => p.DailyDigestEnabled).Select(p => p.UserId).ToListAsync();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
        foreach (var user in users) { jobs.Enqueue<DailyDigestJob>(job => job.RunAsync(officeId, user, day)); }
    }

    private static async Task AddRemindersAsync(PlatformDbContext db, IFatalDeadlineReminders deadlines, DateOnly day, DateTimeOffset now)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var key = "fatal-day:" + db.CurrentOrganizationId!.Value.ToString("N") + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
        foreach (var deadline in await deadlines.DueAsync(day, CancellationToken.None))
        {
            var notificationKey = key + ":" + deadline.Id.ToString("N");
            if (!await db.Notifications.AnyAsync(n => n.Key == notificationKey))
            {
                db.Notifications.Add(Notification.Create(notificationKey, "scheduling.deadline_due_today", deadline.Id, deadline.CaseId, now));
            }
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}

internal static class DailySchedulingTime
{
    public static DateOnly Day(IClock clock) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, clock.LegalTimeZone).DateTime);
    public static bool Ready(IClock clock, DateOnly day) => day == Day(clock)
        && TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, clock.LegalTimeZone).DateTime) >= new TimeOnly(7, 30);
}

using System.Globalization;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Integrations;
using LexFlow.Identity.Contracts;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Organization.Contracts.OrgStructure;
using LexFlow.Platform.Contracts.Notifications;
using LexFlow.Platform.Domain.Notifications;
using LexFlow.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Platform.Infrastructure.Notifications;

public sealed class DailyDigestJob(IServiceScopeFactory scopes)
{
    public async Task RunAsync(Guid officeId, Guid userId, DateOnly day)
    {
        using var ambient = AmbientContext.Begin(officeId, "daily-digest");
        await using var scope = scopes.CreateAsyncScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<TenantOverride>().BeginForBackgroundJob(officeId);
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        if (!DailySchedulingTime.Ready(clock, day)) { return; }
        var directory = scope.ServiceProvider.GetRequiredService<IOrganizationAccessDirectory>();
        if (await directory.GetAsync(userId, CancellationToken.None) is null) { return; }
        var user = await scope.ServiceProvider.GetRequiredService<IUserDirectory>().FindAsync(userId, CancellationToken.None);
        if (user is null || !user.EmailConfirmed) { return; }
        await SendAsync(scope.ServiceProvider, user, day, clock);
    }

    private static async Task SendAsync(IServiceProvider services, UserSummary user, DateOnly day, IClock clock)
    {
        var db = services.GetRequiredService<PlatformDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var key = "daily-digest:" + db.CurrentOrganizationId!.Value.ToString("N") + user.Id.ToString("N") + day.ToString("O", CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
        var preference = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == user.Id);
        if (preference is null || !preference.DailyDigestEnabled) { return; }
        var delivery = await db.DigestDeliveries.SingleOrDefaultAsync(d => d.UserId == user.Id && d.Day == day);
        if (delivery?.SentAt is not null) { return; }
        var sections = new List<DigestSection>();
        foreach (var source in services.GetServices<IDailyDigestSource>())
        {
            sections.AddRange((await source.ReadAsync(user.Id, day, CancellationToken.None)).Where(s => Included(preference, s.Kind)));
        }
        sections.RemoveAll(section => section.Count == 0);
        if (sections.Count == 0) { return; }
        // Revalida a pertença antes da exportação; receber o e-mail nunca concede acesso ao sistema.
        if (await services.GetRequiredService<IOrganizationAccessDirectory>().GetAsync(user.Id, CancellationToken.None) is null) { return; }
        await services.GetRequiredService<IEmailSender>().SendAsync(DailyDigestMessage.Create(user.Email, day, sections), CancellationToken.None);
        if (delivery is null) { delivery = DigestDelivery.Create(user.Id, day); db.DigestDeliveries.Add(delivery); }
        delivery.MarkSent(clock.UtcNow);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static bool Included(NotificationPreference preference, string kind) => kind switch
    {
        "agenda" => preference.IncludeAgenda,
        "deadlines" => preference.IncludeDeadlines,
        "publications" => preference.IncludePublications,
        _ => false,
    };
}

using LexFlow.Scheduling.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

/// <summary>Diretório usado pelos consumidores de alertas sem expor o contexto de persistência do Scheduling.</summary>
internal sealed class DeadlineAlertDirectory(ISchedulingDbContext db)
    : IDeadlineAlertDirectory
{
    public Task<DeadlineAlertDetails?> FindAsync(Guid activityId, CancellationToken cancellationToken) =>
        (from activity in db.DeadlineActivities.AsNoTracking()
         join deadline in db.Deadlines.AsNoTracking() on activity.DeadlineId equals deadline.Id
         where activity.Id == activityId
         select new DeadlineAlertDetails(activity.Id, deadline.Id, deadline.LegalCaseId, activity.Kind,
             deadline.AssigneeUserId, deadline.ReviewedBy))
        .SingleOrDefaultAsync(cancellationToken);

}

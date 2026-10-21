using LexFlow.Scheduling.Contracts;
using LexFlow.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

internal sealed class FatalDeadlineReminders(ISchedulingDbContext db) : IFatalDeadlineReminders
{
    public async Task<IReadOnlyList<FatalDeadlineReminder>> DueAsync(DateOnly day, CancellationToken cancellationToken)
    {
        return await db.Deadlines.AsNoTracking()
            .Where(deadline => deadline.FatalDueOn == day
                && deadline.Status != WorkItemStatus.Completed
                && deadline.Status != WorkItemStatus.Missed)
            .OrderBy(deadline => deadline.Id)
            .Select(deadline => new FatalDeadlineReminder(deadline.Id, deadline.LegalCaseId))
            .ToListAsync(cancellationToken);
    }
}

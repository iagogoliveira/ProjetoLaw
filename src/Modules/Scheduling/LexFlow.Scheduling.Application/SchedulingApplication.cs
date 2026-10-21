using LexFlow.Application.Abstractions.Data;
using LexFlow.Scheduling.Domain;
using LexFlow.Scheduling.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Scheduling.Application;

public static class SchedulingApplication
{
    public static readonly System.Reflection.Assembly Assembly = typeof(SchedulingApplication).Assembly;
}

public interface ISchedulingDbContext : IUnitOfWork
{
    DbSet<Deadline> Deadlines { get; }
    DbSet<TaskItem> Tasks { get; }
    DbSet<CalendarEntry> CalendarEntries { get; }
    DbSet<DeadlineActivity> DeadlineActivities { get; }
    DbSet<CalendarEvent> Events { get; }
    DbSet<TaskChecklistItem> TaskChecklistItems { get; }
    DbSet<TaskComment> TaskComments { get; }
    Task LockDeadlinesAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

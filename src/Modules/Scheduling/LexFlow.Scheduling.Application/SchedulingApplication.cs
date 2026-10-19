using LexFlow.Application.Abstractions.Data;
using LexFlow.Scheduling.Domain;
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
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

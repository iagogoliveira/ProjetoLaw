using LexFlow.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Scheduling.Infrastructure.Persistence;

internal sealed class AgendaConfigurations : IEntityTypeConfiguration<CalendarEvent>,
    IEntityTypeConfiguration<TaskChecklistItem>, IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Title).HasMaxLength(300);
        builder.Property(e => e.Description).HasMaxLength(4000);
        builder.Property(e => e.Location).HasMaxLength(500);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(e => new { e.OrganizationId, e.StartsAt });
        builder.HasIndex(e => new { e.OrganizationId, e.AssigneeUserId, e.StartsAt });
    }

    public void Configure(EntityTypeBuilder<TaskChecklistItem> builder)
    {
        builder.ToTable("task_checklist_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Title).HasMaxLength(300);
        builder.HasIndex(e => new { e.OrganizationId, e.TaskId });
    }

    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("task_comments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Text).HasMaxLength(4000);
        builder.HasIndex(e => new { e.OrganizationId, e.TaskId, e.OccurredAt });
    }
}

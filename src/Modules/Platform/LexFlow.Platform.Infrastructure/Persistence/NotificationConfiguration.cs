using LexFlow.Platform.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Platform.Infrastructure.Persistence;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>, IEntityTypeConfiguration<NotificationReceipt>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Key).HasMaxLength(200);
        builder.Property(n => n.Kind).HasMaxLength(100);
        builder.HasIndex(n => new { n.OrganizationId, n.Key }).IsUnique();
        builder.HasIndex(n => new { n.OrganizationId, n.CreatedAt, n.Id });
    }
    public void Configure(EntityTypeBuilder<NotificationReceipt> builder)
    {
        builder.ToTable("notification_receipts");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.HasOne<Notification>().WithMany().HasForeignKey(n => n.NotificationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(n => new { n.OrganizationId, n.NotificationId, n.UserId }).IsUnique();
    }
}

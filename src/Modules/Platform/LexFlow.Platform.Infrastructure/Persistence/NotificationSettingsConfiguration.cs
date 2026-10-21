using LexFlow.Platform.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Platform.Infrastructure.Persistence;

internal sealed class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationPreference>,
    IEntityTypeConfiguration<NotificationRule>, IEntityTypeConfiguration<DigestDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasIndex(p => new { p.OrganizationId, p.UserId }).IsUnique();
    }
    public void Configure(EntityTypeBuilder<NotificationRule> builder)
    {
        builder.ToTable("notification_rules");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasIndex(p => p.OrganizationId).IsUnique();
    }
    public void Configure(EntityTypeBuilder<DigestDelivery> builder)
    {
        builder.ToTable("digest_deliveries");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasIndex(p => new { p.OrganizationId, p.UserId, p.Day }).IsUnique();
    }
}

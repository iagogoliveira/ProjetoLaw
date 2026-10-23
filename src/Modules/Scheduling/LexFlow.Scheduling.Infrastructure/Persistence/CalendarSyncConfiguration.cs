using LexFlow.Scheduling.Domain.CalendarSync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Scheduling.Infrastructure.Persistence;

internal sealed class CalendarSyncConfiguration : IEntityTypeConfiguration<CalendarConnection>, IEntityTypeConfiguration<CalendarSyncLink>, IEntityTypeConfiguration<SimulatedCalendarItem>
{
    public void Configure(EntityTypeBuilder<SimulatedCalendarItem> builder)
    {
        builder.ToTable("simulated_calendar_items");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Provider).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.RemoteId).HasMaxLength(200);
        builder.Property(e => e.ClientKey).HasMaxLength(200);
        builder.Property(e => e.Payload).HasColumnType("jsonb");
        builder.Property(e => e.Link).HasMaxLength(2000);
        builder.HasIndex(e => new { e.OrganizationId, e.UserId, e.Provider, e.RemoteId }).IsUnique();
        builder.HasIndex(e => new { e.OrganizationId, e.UserId, e.Provider, e.ClientKey }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<CalendarConnection> builder)
    {
        builder.ToTable("calendar_connections");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Provider).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.LastErrorCode).HasMaxLength(100);
        builder.HasIndex(c => new { c.OrganizationId, c.UserId, c.Provider }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<CalendarSyncLink> builder)
    {
        builder.ToTable("calendar_sync_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.PendingReason).HasConversion<string>().HasMaxLength(30);
        builder.Property(l => l.RemoteId).HasMaxLength(200);
        builder.Property(l => l.RemoteVersion).HasMaxLength(200);
        builder.Property(l => l.RemoteSnapshot).HasColumnType("jsonb");
        builder.Property(l => l.PendingSnapshot).HasColumnType("jsonb");
        builder.Property(l => l.LocalFingerprint).HasMaxLength(64);
        builder.Property(l => l.PendingLocalFingerprint).HasMaxLength(64);
        builder.HasOne<CalendarConnection>().WithMany().HasForeignKey(l => l.ConnectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(l => new { l.OrganizationId, l.ConnectionId, l.RemoteId }).IsUnique();
        builder.HasIndex(l => new { l.OrganizationId, l.ConnectionId, l.Kind, l.LocalId }).IsUnique();
    }
}

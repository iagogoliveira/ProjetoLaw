using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Publications.Infrastructure.Persistence;

internal sealed class CaptureAlertConfiguration : IEntityTypeConfiguration<CaptureAlert>
{
    public void Configure(EntityTypeBuilder<CaptureAlert> builder)
    {
        builder.ToTable("capture_alerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.ErrorCode).HasMaxLength(100);
        builder.HasOne<CaptureSource>().WithMany().HasForeignKey(a => a.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.OrganizationId, a.SourceId, a.Day }).IsUnique();
    }
}

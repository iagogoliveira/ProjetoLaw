using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Publications.Infrastructure.Persistence;

internal sealed class ControlConfigurations : IEntityTypeConfiguration<DailyClose>, IEntityTypeConfiguration<CaptureSourcePeriod>
{
    public void Configure(EntityTypeBuilder<DailyClose> builder)
    {
        builder.ToTable("daily_closes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.CaptureFingerprint).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(c => new { c.OrganizationId, c.Day, c.Revision }).IsUnique();
    }
    public void Configure(EntityTypeBuilder<CaptureSourcePeriod> builder)
    {
        builder.ToTable("capture_source_periods");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.HasOne<CaptureSource>().WithMany().HasForeignKey(p => p.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.OrganizationId, p.SourceId, p.ActivatedAt });
    }
}

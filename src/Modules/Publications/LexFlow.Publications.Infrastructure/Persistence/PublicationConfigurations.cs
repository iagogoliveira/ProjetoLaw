using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Publications.Infrastructure.Persistence;

internal sealed class PublicationConfigurations :
    IEntityTypeConfiguration<CaptureSource>, IEntityTypeConfiguration<CaptureRun>,
    IEntityTypeConfiguration<Publication>, IEntityTypeConfiguration<PublicationTriage>, IEntityTypeConfiguration<CaseMovement>
{
    public void Configure(EntityTypeBuilder<CaptureSource> builder)
    {
        builder.ToTable("capture_sources");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Label).HasMaxLength(200);
        builder.Property(s => s.Value).HasMaxLength(200);
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(s => new { s.OrganizationId, s.IsActive });
    }

    public void Configure(EntityTypeBuilder<CaptureRun> builder)
    {
        builder.ToTable("capture_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ErrorCode).HasMaxLength(100);
        builder.HasOne<CaptureSource>().WithMany().HasForeignKey(r => r.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CaptureSourcePeriod>().WithMany().HasForeignKey(r => r.SourcePeriodId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.OrganizationId, r.SourceId, r.Day });
    }

    public void Configure(EntityTypeBuilder<Publication> builder)
    {
        builder.ToTable("publications");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Journal).HasMaxLength(200);
        builder.Property(p => p.Text).HasMaxLength(200_000);
        builder.Property(p => p.Hash).HasMaxLength(64).IsFixedLength();
        builder.Property(p => p.ExtractedCnj).HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<CaptureSource>().WithMany().HasForeignKey(p => p.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.OrganizationId, p.Hash }).IsUnique();
        builder.HasIndex(p => new { p.OrganizationId, p.Status, p.ReceivedAt, p.Id });
        builder.HasIndex(p => new { p.OrganizationId, p.LegalCaseId });
    }

    public void Configure(EntityTypeBuilder<PublicationTriage> builder)
    {
        builder.ToTable("publication_triages");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Classification).HasConversion<string>().HasMaxLength(30);
        builder.Property(t => t.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Reason).HasMaxLength(4000);
        builder.HasOne<Publication>().WithMany().HasForeignKey(t => t.PublicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.OrganizationId, t.PublicationId }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<CaseMovement> builder)
    {
        builder.ToTable("case_movements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Description).HasMaxLength(20_000);
        builder.Property(m => m.Origin).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.ExternalId).HasMaxLength(128);
        builder.HasIndex(m => new { m.OrganizationId, m.LegalCaseId, m.Origin, m.ExternalId }).IsUnique();
        builder.HasIndex(m => new { m.OrganizationId, m.LegalCaseId, m.OccurredAt, m.Id });
    }
}

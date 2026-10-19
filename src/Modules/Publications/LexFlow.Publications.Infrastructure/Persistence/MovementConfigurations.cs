using LexFlow.Publications.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Publications.Infrastructure.Persistence;

internal sealed class MovementConfigurations : IEntityTypeConfiguration<MovementIntegration>, IEntityTypeConfiguration<MovementSync>
{
    public void Configure(EntityTypeBuilder<MovementIntegration> builder)
    {
        builder.ToTable("movement_integrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ProtectedApiKey).HasMaxLength(4096);
        builder.HasIndex(x => x.OrganizationId).IsUnique();
    }
    public void Configure(EntityTypeBuilder<MovementSync> builder)
    {
        builder.ToTable("movement_syncs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Tribunal).HasMaxLength(40);
        builder.Property(x => x.Cursor).HasMaxLength(4096);
        builder.Property(x => x.ErrorCode).HasMaxLength(100);
        builder.HasIndex(x => new { x.OrganizationId, x.LegalCaseId }).IsUnique();
    }
}

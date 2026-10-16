using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Platform.Domain.Calendar;
using LexFlow.Platform.Domain.Courts;
using LexFlow.Platform.Domain.Idempotency;
using LexFlow.Platform.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Platform.Infrastructure.Persistence;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Type).HasMaxLength(100);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.ResultLocation).HasMaxLength(500);
        builder.Property(j => j.ErrorCode).HasMaxLength(100);
        builder.HasIndex(j => new { j.OrganizationId, j.CreatedAt });
        builder.HasStandardColumns();
    }
}

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Key).HasMaxLength(IdempotencyKey.MaxKeyLength);
        builder.Property(k => k.RequestHash).HasMaxLength(64).IsFixedLength();
        builder.Property(k => k.ResponseContentType).HasMaxLength(200);
        builder.Ignore(k => k.IsCompleted);

        // Unicidade dentro do tenant (seção 6.1); índice por expiração para a limpeza periódica.
        builder.HasIndex(k => new { k.OrganizationId, k.Key }).IsUnique();
        builder.HasIndex(k => k.ExpiresAt);
    }
}

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Name).HasMaxLength(200);
        builder.Property(h => h.LegalBasis).HasMaxLength(200);
        builder.Property(h => h.Scope).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Rule).HasConversion<string>().HasMaxLength(20);
        builder.HasData(ReferenceData.NationalHolidays);
    }
}

internal sealed class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Code).HasMaxLength(2).IsFixedLength();
        builder.Property(c => c.Acronym).HasMaxLength(20);
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.State).HasMaxLength(2).IsFixedLength();
        builder.HasIndex(c => new { c.Segment, c.Code }).IsUnique();
        builder.HasData(ReferenceData.Courts);
    }
}

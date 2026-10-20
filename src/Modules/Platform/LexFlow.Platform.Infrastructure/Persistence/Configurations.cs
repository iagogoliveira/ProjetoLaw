using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Platform.Domain.Calendar;
using LexFlow.Platform.Domain.Courts;
using LexFlow.Platform.Domain.CustomFields;
using LexFlow.Platform.Domain.Idempotency;
using LexFlow.Platform.Domain.Indexes;
using LexFlow.Platform.Domain.Jobs;
using LexFlow.Platform.Domain.Preferences;
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
        builder.Property(h => h.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.StateCode).HasMaxLength(2).IsFixedLength();
        builder.Property(h => h.SpanDays).HasDefaultValue(1);
        builder.HasIndex(h => new { h.Scope, h.StateCode });
        builder.HasData(ReferenceData.Holidays);
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

/// <summary>Preferências de tela: uma por usuário, escritório e chave.</summary>
internal sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.ToTable("user_preferences");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Key).HasMaxLength(UserPreference.MaxKeyLength);
        builder.Property(p => p.Value).HasColumnType("jsonb");
        builder.HasIndex(p => new { p.OrganizationId, p.UserId, p.Key }).IsUnique();
    }
}

/// <summary>Índices monetários (seção 6.5): um valor por índice e mês, com a origem. O seed entra por migration de dados.</summary>
internal sealed class MonetaryIndexConfiguration : IEntityTypeConfiguration<MonetaryIndex>
{
    public void Configure(EntityTypeBuilder<MonetaryIndex> builder)
    {
        builder.ToTable("monetary_indexes");
        builder.HasKey(i => new { i.Kind, i.Month });
        builder.Property(i => i.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(i => i.Percent).HasPrecision(9, 4);
        builder.Property(i => i.Source).HasMaxLength(MonetaryIndex.MaxSourceLength);
    }
}

/// <summary>Chave única por entidade dentro do escritório, inclusive entre campos inativos (os valores usam a chave).</summary>
internal sealed class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.ToTable("custom_field_definitions");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Entity).HasMaxLength(50);
        builder.Property(f => f.Key).HasMaxLength(CustomFieldDefinition.MaxKeyLength);
        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Label).HasMaxLength(CustomFieldDefinition.MaxLabelLength);
        builder.Property(f => f.HelpText).HasMaxLength(CustomFieldDefinition.MaxHelpTextLength);
        builder.Property(f => f.Minimum).HasPrecision(19, 4);
        builder.Property(f => f.Maximum).HasPrecision(19, 4);
        builder.Ignore(f => f.Settings);
        builder.HasIndex(f => new { f.OrganizationId, f.Entity, f.Key }).IsUnique();
        builder.HasOne<LookupTable>().WithMany().HasForeignKey(f => f.LookupTableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasStandardColumns();
    }
}

internal sealed class LookupTableConfiguration : IEntityTypeConfiguration<LookupTable>
{
    public void Configure(EntityTypeBuilder<LookupTable> builder)
    {
        builder.ToTable("lookup_tables");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(LookupTable.MaxNameLength);
        builder.Property(t => t.SystemKey).HasMaxLength(50);
        builder.HasIndex(t => new { t.OrganizationId, t.Name }).IsUnique();
        builder.HasIndex(t => new { t.OrganizationId, t.SystemKey }).IsUnique().HasFilter("system_key IS NOT NULL");
        builder.HasStandardColumns();
    }
}

internal sealed class LookupItemConfiguration : IEntityTypeConfiguration<LookupItem>
{
    public void Configure(EntityTypeBuilder<LookupItem> builder)
    {
        builder.ToTable("lookup_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).HasMaxLength(LookupItem.MaxNameLength);
        builder.HasIndex(i => new { i.OrganizationId, i.TableId });
        builder.HasOne<LookupTable>().WithMany().HasForeignKey(i => i.TableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasStandardColumns();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Common.Persistence.Auditing;

/// <summary>Ações registradas na trilha de auditoria.</summary>
public static class AuditActions
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
    public const string AccessDenied = "AccessDenied";
}

/// <summary>
/// Linha de <c>audit.audit_logs</c> (seção 6.4), só de inserção. As colunas de encadeamento
/// (<c>chain_position</c>, <c>previous_hash</c>, <c>hash</c>) são calculadas pelo trigger do banco e não são mapeadas aqui.
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; init; }

    public Guid? OrganizationId { get; init; }

    public Guid? UserId { get; init; }

    public required string Action { get; init; }

    public required string EntityType { get; init; }

    public required string EntityId { get; init; }

    /// <summary>JSON com antes/depois dos campos não sensíveis.</summary>
    public required string Changes { get; init; }

    public string? Ip { get; init; }

    public string? UserAgent { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}

internal sealed class AuditLogEntryConfiguration(bool excludeFromMigrations) : IEntityTypeConfiguration<AuditLogEntry>
{
    public const string Schema = "audit";
    public const string Table = "audit_logs";

    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable(Table, Schema, t =>
        {
            if (excludeFromMigrations)
            {
                t.ExcludeFromMigrations();
            }
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Action).HasMaxLength(64);
        builder.Property(e => e.EntityType).HasMaxLength(200);
        builder.Property(e => e.EntityId).HasMaxLength(200);
        builder.Property(e => e.Changes).HasColumnType("jsonb");
        builder.Property(e => e.Ip).HasMaxLength(64);
        builder.Property(e => e.UserAgent).HasMaxLength(512);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
    }
}

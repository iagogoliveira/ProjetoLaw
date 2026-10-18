using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Domain.Numbering;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Litigation.Infrastructure.Persistence;

/// <summary>DbContext do módulo Litigation (schema <c>litigation</c>): processos e casos, partes, responsáveis e hierarquia.</summary>
public sealed class LitigationDbContext(DbContextOptions<LitigationDbContext> options, ITenantContext tenant)
    : ModuleDbContextBase(options, tenant), ILitigationDbContext
{
    public const string SchemaName = "litigation";
    public const string ModuleName = "Litigation";

    public DbSet<LegalCase> Cases => Set<LegalCase>();

    public DbSet<CaseRelation> Relations => Set<CaseRelation>();

    public DbSet<CaseNumbering> Numberings => Set<CaseNumbering>();

    internal DbSet<CaseCounter> Counters => Set<CaseCounter>();

    protected override string Schema => SchemaName;
}

/// <summary>Contador da numeração por organização, área (<see cref="Guid.Empty"/> sem área) e ano.</summary>
internal sealed class CaseCounter : ITenantOwned
{
    public Guid OrganizationId { get; private set; }

    public Guid PracticeAreaKey { get; private set; }

    public int Year { get; private set; }

    public long LastValue { get; private set; }
}

/// <summary>
/// Pasta interna única no escritório; número CNJ único entre os processos ativos; índices para a lista (data, situação
/// e área) e para achar as partes de uma pessoa (conflito de interesses e fusão de cadastros).
/// </summary>
internal sealed class CaseConfiguration : IEntityTypeConfiguration<LegalCase>, IEntityTypeConfiguration<CaseParty>, IEntityTypeConfiguration<CaseAssignment>
{
    public void Configure(EntityTypeBuilder<LegalCase> builder)
    {
        builder.ToTable("cases");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.InternalFolder).HasMaxLength(80);
        builder.Property(c => c.CnjNumber).HasMaxLength(20).IsFixedLength();
        builder.Property(c => c.Instance).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.District).HasMaxLength(LegalCase.MaxTextLength);
        builder.Property(c => c.CourtUnit).HasMaxLength(LegalCase.MaxTextLength);
        builder.Property(c => c.Rite).HasMaxLength(LegalCase.MaxTextLength);
        builder.Property(c => c.ProceduralClass).HasMaxLength(LegalCase.MaxTextLength);
        builder.Property(c => c.Subject).HasMaxLength(LegalCase.MaxTextLength);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.ClaimValue).HasPrecision(19, 4);
        builder.Property(c => c.Description).HasMaxLength(LegalCase.MaxDescriptionLength);
        builder.Property(c => c.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.FinalAmount).HasPrecision(19, 4);
        builder.Property(c => c.ClosingNote).HasMaxLength(LegalCase.MaxNoteLength);
        builder.Property(c => c.CustomFields).HasColumnType("jsonb");
        builder.HasIndex(c => new { c.OrganizationId, c.InternalFolder }).IsUnique();
        builder.HasIndex(c => new { c.OrganizationId, c.CnjNumber }).IsUnique().HasFilter("cnj_number IS NOT NULL AND NOT is_deleted");
        builder.HasIndex(c => new { c.OrganizationId, c.CreatedAt });
        builder.HasIndex(c => new { c.OrganizationId, c.Status, c.PracticeAreaId });
        builder.HasMany(c => c.Parties).WithOne().HasForeignKey(p => p.LegalCaseId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Assignments).WithOne().HasForeignKey(a => a.LegalCaseId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Parties).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Assignments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<CaseParty> builder)
    {
        builder.ToTable("case_parties");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Pole).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.CounselName).HasMaxLength(CaseParty.MaxCounselLength);
        builder.Property(p => p.CounselOab).HasMaxLength(20);
        builder.HasIndex(p => new { p.OrganizationId, p.PersonId });
        builder.HasIndex(p => p.LegalCaseId);
    }

    public void Configure(EntityTypeBuilder<CaseAssignment> builder)
    {
        builder.ToTable("case_assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => new { a.OrganizationId, a.UserId });
        builder.HasIndex(a => new { a.OrganizationId, a.TeamId });
        builder.HasIndex(a => a.LegalCaseId);
    }
}

internal sealed class CaseSupportConfiguration :
    IEntityTypeConfiguration<CaseRelation>,
    IEntityTypeConfiguration<CaseNumbering>,
    IEntityTypeConfiguration<CaseCounter>
{
    public void Configure(EntityTypeBuilder<CaseRelation> builder)
    {
        builder.ToTable("case_relations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<LegalCase>().WithMany().HasForeignKey(r => r.ParentCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LegalCase>().WithMany().HasForeignKey(r => r.ChildCaseId).OnDelete(DeleteBehavior.Restrict);

        // Um processo tem no máximo um principal.
        builder.HasIndex(r => new { r.OrganizationId, r.ChildCaseId }).IsUnique();
        builder.HasIndex(r => new { r.OrganizationId, r.ParentCaseId });
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<CaseNumbering> builder)
    {
        builder.ToTable("case_numberings");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Format).HasMaxLength(CaseNumbering.MaxFormatLength);
        builder.Property(n => n.AreaCodes).HasColumnType("jsonb");
        builder.Ignore(n => n.Codes);
        builder.HasIndex(n => n.OrganizationId).IsUnique();
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<CaseCounter> builder)
    {
        builder.ToTable("case_counters");
        builder.HasKey(c => new { c.OrganizationId, c.PracticeAreaKey, c.Year });
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c>; nunca conecta ao banco.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<LitigationDbContext>
{
    public LitigationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LitigationDbContext>()
            .UseNpgsql("Host=localhost;Database=lexflow_design", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", LitigationDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new LitigationDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? OrganizationId => null;
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain.CaseImports;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Domain.Claims;
using LexFlow.Litigation.Domain.Hearings;
using LexFlow.Litigation.Domain.Numbering;
using LexFlow.Litigation.Domain.Risk;
using LexFlow.Litigation.Domain.Settlements;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Litigation.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Litigation (schema <c>litigation</c>): processos e casos, partes, responsáveis, hierarquia e
/// pedidos com os valores por instância.
/// </summary>
public sealed class LitigationDbContext(DbContextOptions<LitigationDbContext> options, ITenantContext tenant)
    : ModuleDbContextBase(options, tenant), ILitigationDbContext
{
    public const string SchemaName = "litigation";
    public const string ModuleName = "Litigation";

    public DbSet<LegalCase> Cases => Set<LegalCase>();

    public DbSet<CaseRelation> Relations => Set<CaseRelation>();

    public DbSet<CaseNumbering> Numberings => Set<CaseNumbering>();

    public DbSet<Claim> Claims => Set<Claim>();

    public DbSet<CaseStatusChange> StatusChanges => Set<CaseStatusChange>();

    public DbSet<CaseImport> CaseImports => Set<CaseImport>();

    public DbSet<CaseImportError> ImportErrors => Set<CaseImportError>();

    public DbSet<RiskAssessment> RiskAssessments => Set<RiskAssessment>();

    public DbSet<Settlement> Settlements => Set<Settlement>();

    public DbSet<Hearing> Hearings => Set<Hearing>();

    internal DbSet<CaseCounter> Counters => Set<CaseCounter>();

    protected override string Schema => SchemaName;

    public void ClearTracking() => ChangeTracker.Clear();
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
internal sealed class CaseConfiguration :
    IEntityTypeConfiguration<LegalCase>,
    IEntityTypeConfiguration<CaseParty>,
    IEntityTypeConfiguration<CaseAssignment>,
    IEntityTypeConfiguration<CaseRestriction>
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
        builder.HasMany(c => c.Restrictions).WithOne().HasForeignKey(r => r.LegalCaseId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Restrictions).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Lista por área (o filtro do critério de aceite) e por filial, na ordem da lista.
        builder.HasIndex(c => new { c.OrganizationId, c.PracticeAreaId, c.CreatedAt });
        builder.HasIndex(c => new { c.OrganizationId, c.BranchId, c.CreatedAt });
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<CaseParty> builder)
    {
        builder.ToTable("case_parties");
        builder.HasKey(p => p.Id);
        // Id gerado no domínio: filha nova numa edição precisa entrar como inserção, não como alteração.
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Pole).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.CounselName).HasMaxLength(CaseParty.MaxCounselLength);
        builder.Property(p => p.CounselOab).HasMaxLength(20);
        builder.HasIndex(p => new { p.OrganizationId, p.PersonId });
        builder.HasIndex(p => p.LegalCaseId);
    }

    public void Configure(EntityTypeBuilder<CaseRestriction> builder)
    {
        builder.ToTable("case_restrictions");
        builder.HasKey(r => r.Id);
        // Id gerado no domínio: filha nova numa edição precisa entrar como inserção, não como alteração.
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Mode).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(r => r.LegalCaseId);
    }

    public void Configure(EntityTypeBuilder<CaseAssignment> builder)
    {
        builder.ToTable("case_assignments");
        builder.HasKey(a => a.Id);
        // Id gerado no domínio: filha nova numa edição precisa entrar como inserção, não como alteração.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => new { a.OrganizationId, a.UserId });
        builder.HasIndex(a => new { a.OrganizationId, a.TeamId });
        builder.HasIndex(a => a.LegalCaseId);
    }
}

internal sealed class CaseSupportConfiguration :
    IEntityTypeConfiguration<CaseRelation>,
    IEntityTypeConfiguration<CaseStatusChange>,
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

    public void Configure(EntityTypeBuilder<CaseStatusChange> builder)
    {
        builder.ToTable("case_status_changes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.From).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.To).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Note).HasMaxLength(CaseStatusChange.MaxNoteLength);
        builder.HasOne<LegalCase>().WithMany().HasForeignKey(c => c.LegalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.LegalCaseId);
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

/// <summary>
/// Pedidos do processo, com os valores por instância (um por instância) e o histórico das alterações de valor, que
/// só recebe inserções (o UPDATE e o DELETE são revogados da aplicação na migration).
/// </summary>
internal sealed class ClaimConfiguration :
    IEntityTypeConfiguration<Claim>,
    IEntityTypeConfiguration<ClaimValuation>,
    IEntityTypeConfiguration<ClaimValueChange>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("claims");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Description).HasMaxLength(Claim.MaxDescriptionLength);
        builder.Property(c => c.CorrectionIndex).HasConversion<string>().HasMaxLength(10);
        builder.Property(c => c.MonthlyInterestPercent).HasPrecision(7, 4);
        builder.HasOne<LegalCase>().WithMany().HasForeignKey(c => c.LegalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.Valuations).WithOne().HasForeignKey(v => v.ClaimId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(c => c.Changes).WithOne().HasForeignKey(v => v.ClaimId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(c => c.Valuations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(c => c.Changes).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(c => new { c.OrganizationId, c.LegalCaseId });
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<ClaimValuation> builder)
    {
        builder.ToTable("claim_valuations");
        builder.HasKey(v => v.Id);
        // Id gerado no domínio: filha nova numa edição precisa entrar como inserção, não como alteração.
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Instance).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.Requested).HasPrecision(19, 4);
        builder.Property(v => v.Estimated).HasPrecision(19, 4);
        builder.Property(v => v.Awarded).HasPrecision(19, 4);
        builder.HasIndex(v => new { v.ClaimId, v.Instance }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<ClaimValueChange> builder)
    {
        builder.ToTable("claim_value_changes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Instance).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.OldAmount).HasPrecision(19, 4);
        builder.Property(c => c.NewAmount).HasPrecision(19, 4);
        builder.HasIndex(c => c.ClaimId);
    }
}

/// <summary>
/// Risco (só inserções, como o histórico dos pedidos), acordos com rodadas e parcelas, e audiências com o histórico
/// das redesignações.
/// </summary>
internal sealed class CaseFollowUpConfiguration :
    IEntityTypeConfiguration<RiskAssessment>,
    IEntityTypeConfiguration<Settlement>,
    IEntityTypeConfiguration<SettlementOffer>,
    IEntityTypeConfiguration<SettlementInstallment>,
    IEntityTypeConfiguration<Hearing>,
    IEntityTypeConfiguration<HearingReschedule>
{
    public void Configure(EntityTypeBuilder<RiskAssessment> builder)
    {
        builder.ToTable("risk_assessments");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Probability).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.EstimatedLoss).HasPrecision(19, 4);
        builder.Property(r => r.Rationale).HasMaxLength(RiskAssessment.MaxRationaleLength);
        builder.Ignore(r => r.Accounting);
        builder.HasOne<LegalCase>().WithMany().HasForeignKey(r => r.LegalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.OrganizationId, r.LegalCaseId, r.AssessedAt });
    }

    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("settlements");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Direction).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Notes).HasMaxLength(Settlement.MaxTextLength);
        builder.Property(s => s.Reason).HasMaxLength(Settlement.MaxTextLength);
        builder.Property(s => s.AgreedAmount).HasPrecision(19, 4);
        builder.Ignore(s => s.IsOpen);
        builder.HasOne<LegalCase>().WithMany().HasForeignKey(s => s.LegalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(s => s.Offers).WithOne().HasForeignKey(o => o.SettlementId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Installments).WithOne().HasForeignKey(i => i.SettlementId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Offers).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(s => s.Installments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(s => new { s.OrganizationId, s.LegalCaseId });
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<SettlementOffer> builder)
    {
        builder.ToTable("settlement_offers");
        builder.HasKey(o => o.Id);
        // Id gerado no domínio: filha nova numa edição precisa entrar como inserção, não como alteração.
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Party).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Amount).HasPrecision(19, 4);
        builder.Property(o => o.Terms).HasMaxLength(Settlement.MaxTextLength);
        builder.HasIndex(o => new { o.SettlementId, o.Round }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<SettlementInstallment> builder)
    {
        builder.ToTable("settlement_installments");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Amount).HasPrecision(19, 4);
        builder.Property(i => i.PaidAmount).HasPrecision(19, 4);
        builder.HasIndex(i => new { i.SettlementId, i.Number }).IsUnique();
        builder.HasIndex(i => new { i.OrganizationId, i.DueOn });
    }

    public void Configure(EntityTypeBuilder<Hearing> builder)
    {
        builder.ToTable("hearings");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Format).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Location).HasMaxLength(Hearing.MaxTextLength);
        builder.Property(h => h.MeetingLink).HasMaxLength(Hearing.MaxTextLength);
        builder.Property(h => h.Representative).HasMaxLength(Hearing.MaxTextLength);
        builder.Property(h => h.Notes).HasMaxLength(Hearing.MaxNotesLength);
        builder.Property(h => h.Outcome).HasMaxLength(Hearing.MaxNotesLength);
        builder.PrimitiveCollection(h => h.WitnessPersonIds);
        builder.HasOne<LegalCase>().WithMany().HasForeignKey(h => h.LegalCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(h => h.Reschedules).WithOne().HasForeignKey(r => r.HearingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(h => h.Reschedules).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(h => new { h.OrganizationId, h.LegalCaseId, h.ScheduledAt });
        builder.HasIndex(h => new { h.OrganizationId, h.ScheduledAt });
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<HearingReschedule> builder)
    {
        builder.ToTable("hearing_reschedules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Reason).HasMaxLength(Hearing.MaxTextLength);
        builder.HasIndex(r => r.HearingId);
    }
}

/// <summary>Importações de processos e os erros das linhas (consultados pela importação, na ordem das linhas).</summary>
internal sealed class CaseImportConfiguration : IEntityTypeConfiguration<CaseImport>, IEntityTypeConfiguration<CaseImportError>
{
    public void Configure(EntityTypeBuilder<CaseImport> builder)
    {
        builder.ToTable("case_imports");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.FileName).HasMaxLength(CaseImport.MaxFileNameLength);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.ErrorCode).HasMaxLength(100);
        builder.Property(i => i.Preview).HasColumnType("jsonb");
        builder.Ignore(i => i.ValidRows);
        builder.HasIndex(i => new { i.OrganizationId, i.CreatedAt });
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<CaseImportError> builder)
    {
        builder.ToTable("case_import_errors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Column).HasMaxLength(40);
        builder.Property(e => e.Value).HasMaxLength(CaseImportError.MaxValueLength);
        builder.Property(e => e.Code).HasMaxLength(100);
        builder.Property(e => e.Message).HasMaxLength(300);
        builder.HasOne<CaseImport>().WithMany().HasForeignKey(e => e.ImportId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.ImportId, e.Row });
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

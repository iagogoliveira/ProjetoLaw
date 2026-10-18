using LexFlow.Application.Abstractions;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain.Conflicts;
using LexFlow.Crm.Domain.Opportunities;
using LexFlow.Crm.Domain.People;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Crm.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Crm (schema <c>crm</c>): pessoas, papéis, endereços, contatos, grupos econômicos, conflito,
/// oportunidades, propostas e modelos de proposta.
/// </summary>
public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenant)
    : ModuleDbContextBase(options, tenant), ICrmDbContext
{
    public const string SchemaName = "crm";
    public const string ModuleName = "Crm";

    public DbSet<Person> People => Set<Person>();

    public DbSet<EconomicGroup> EconomicGroups => Set<EconomicGroup>();

    public DbSet<ConflictCheck> ConflictChecks => Set<ConflictCheck>();

    public DbSet<Opportunity> Opportunities => Set<Opportunity>();

    public DbSet<Proposal> Proposals => Set<Proposal>();

    public DbSet<ProposalTemplate> ProposalTemplates => Set<ProposalTemplate>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        // pg_trgm (instalada pela migration inicial do Platform, no schema public).
        modelBuilder.HasDbFunction(typeof(CrmDbFunctions).GetMethod(nameof(CrmDbFunctions.WordSimilarity))!)
            .HasName("word_similarity").HasSchema("public");
        modelBuilder.HasDbFunction(typeof(CrmDbFunctions).GetMethod(nameof(CrmDbFunctions.Similarity))!)
            .HasName("similarity").HasSchema("public");
    }
}

/// <summary>
/// Documento único no escritório entre os cadastros ativos (critério de aceite da Etapa 4) e índice trigram (GIN) no
/// nome de busca, para a busca aproximada e a detecção de duplicidade.
/// </summary>
internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Name).HasMaxLength(Person.MaxNameLength);
        builder.Property(p => p.AlternateName).HasMaxLength(Person.MaxNameLength);
        builder.Property(p => p.SearchName).HasMaxLength(Person.MaxNameLength * 2 + 1);
        builder.Property(p => p.Document).HasMaxLength(14);
        builder.Property(p => p.Email).HasMaxLength(254);
        builder.Property(p => p.Phone).HasMaxLength(30);
        builder.Property(p => p.Notes).HasMaxLength(4000);
        builder.Property(p => p.CustomFields).HasColumnType("jsonb");
        builder.Property(p => p.AnonymizationReason).HasMaxLength(500);
        builder.Ignore(p => p.IsAnonymized);
        builder.Property(p => p.StateRegistration).HasMaxLength(30);
        builder.Property(p => p.MunicipalRegistration).HasMaxLength(30);
        builder.Ignore(p => p.Fiscal);
        builder.HasIndex(p => new { p.OrganizationId, p.Document }).IsUnique().HasFilter("document IS NOT NULL AND NOT is_deleted");
        builder.HasIndex(p => p.SearchName).HasMethod("gin").HasOperators("gin_trgm_ops");
        builder.HasIndex(p => new { p.OrganizationId, p.SearchName });
        builder.HasOne<EconomicGroup>().WithMany().HasForeignKey(p => p.EconomicGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Roles).WithOne().HasForeignKey(r => r.PersonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Addresses).WithOne().HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Contacts).WithOne().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Addresses).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(p => p.Contacts).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasStandardColumns();
    }
}

internal sealed class PersonPartsConfiguration :
    IEntityTypeConfiguration<PersonRole>,
    IEntityTypeConfiguration<Address>,
    IEntityTypeConfiguration<Contact>,
    IEntityTypeConfiguration<EconomicGroup>
{
    public void Configure(EntityTypeBuilder<PersonRole> builder)
    {
        builder.ToTable("person_roles");
        builder.HasKey(r => new { r.PersonId, r.Role });
        builder.Property(r => r.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(r => new { r.OrganizationId, r.Role });
    }

    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");
        builder.HasKey(a => a.Id);
        // Id gerado no domínio: filha nova numa edição precisa entrar como inserção, não como alteração.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Street).HasMaxLength(200);
        builder.Property(a => a.Number).HasMaxLength(20);
        builder.Property(a => a.Complement).HasMaxLength(100);
        builder.Property(a => a.District).HasMaxLength(100);
        builder.Property(a => a.City).HasMaxLength(100);
        builder.Property(a => a.State).HasMaxLength(2).IsFixedLength();
        builder.Property(a => a.PostalCode).HasMaxLength(8).IsFixedLength();
        builder.HasIndex(a => new { a.OrganizationId, a.PersonId });
    }

    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");
        builder.HasKey(c => c.Id);
        // Id gerado no domínio: filha nova numa edição precisa entrar como inserção, não como alteração.
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Department).HasMaxLength(100);
        builder.Property(c => c.Email).HasMaxLength(254);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.PreferredChannel).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(c => new { c.OrganizationId, c.PersonId });
    }

    public void Configure(EntityTypeBuilder<EconomicGroup> builder)
    {
        builder.ToTable("economic_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(EconomicGroup.MaxNameLength);
        builder.HasIndex(g => new { g.OrganizationId, g.Name }).IsUnique().HasFilter("NOT is_deleted");
        builder.HasStandardColumns();
    }
}

/// <summary>Verificações de conflito (relatório por data) e os achados de cada uma (busca por pessoa, na fusão e na LGPD).</summary>
internal sealed class ConflictConfiguration : IEntityTypeConfiguration<ConflictCheck>, IEntityTypeConfiguration<ConflictCheckHit>
{
    public void Configure(EntityTypeBuilder<ConflictCheck> builder)
    {
        builder.ToTable("conflict_checks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.QueryName).HasMaxLength(Person.MaxNameLength);
        builder.Property(c => c.QueryDocument).HasMaxLength(14);
        builder.Property(c => c.Side).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Risk).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Justification).HasMaxLength(ConflictCheck.MaxJustificationLength);
        builder.HasIndex(c => new { c.OrganizationId, c.CreatedAt });
        builder.HasMany(c => c.Hits).WithOne().HasForeignKey(h => h.CheckId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Hits).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<ConflictCheckHit> builder)
    {
        builder.ToTable("conflict_check_hits");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Risk).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(h => h.MatchedName).HasMaxLength(Person.MaxNameLength + 200);
        builder.Property(h => h.CaseNumber).HasMaxLength(30);
        builder.HasIndex(h => new { h.OrganizationId, h.PersonId });
        builder.HasIndex(h => h.CheckId);
    }
}

/// <summary>Funil (kanban por etapa e por responsável), propostas por oportunidade e por situação, e os modelos.</summary>
internal sealed class CommercialConfiguration :
    IEntityTypeConfiguration<Opportunity>,
    IEntityTypeConfiguration<Proposal>,
    IEntityTypeConfiguration<ProposalItem>,
    IEntityTypeConfiguration<ProposalTemplate>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("opportunities");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Title).HasMaxLength(Opportunity.MaxTitleLength);
        builder.Property(o => o.ProspectKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.ProspectName).HasMaxLength(Person.MaxNameLength);
        builder.Property(o => o.ProspectDocument).HasMaxLength(14);
        builder.Property(o => o.ProspectEmail).HasMaxLength(254);
        builder.Property(o => o.ProspectPhone).HasMaxLength(30);
        builder.Property(o => o.EstimatedValue).HasPrecision(19, 4);
        builder.Property(o => o.Notes).HasMaxLength(Opportunity.MaxNotesLength);
        builder.Property(o => o.Stage).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.LossNote).HasMaxLength(Opportunity.MaxLossNoteLength);
        builder.Property(o => o.CustomFields).HasColumnType("jsonb");
        builder.Ignore(o => o.IsClosed);
        builder.HasOne<Person>().WithMany().HasForeignKey(o => o.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConflictCheck>().WithMany().HasForeignKey(o => o.ConflictCheckId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.OrganizationId, o.Stage, o.CreatedAt });
        builder.HasIndex(o => new { o.OrganizationId, o.ResponsibleUserId });
        builder.HasIndex(o => new { o.OrganizationId, o.PersonId });
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("proposals");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasMaxLength(Proposal.MaxTitleLength);
        builder.Property(p => p.Scope).HasMaxLength(Proposal.MaxTextLength);
        builder.Property(p => p.Conditions).HasMaxLength(Proposal.MaxTextLength);
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.Total).HasPrecision(19, 4);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ReturnNote).HasMaxLength(Proposal.MaxNoteLength);
        builder.Property(p => p.DeclineNote).HasMaxLength(Proposal.MaxNoteLength);
        builder.Ignore(p => p.IsOpen);
        builder.HasOne<Opportunity>().WithMany().HasForeignKey(p => p.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProposalTemplate>().WithMany().HasForeignKey(p => p.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.ProposalId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(p => new { p.OrganizationId, p.OpportunityId });
        builder.HasIndex(p => new { p.OrganizationId, p.Status, p.CreatedAt });
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<ProposalItem> builder)
    {
        builder.ToTable("proposal_items");
        builder.HasKey(i => i.Id);
        // Id gerado no domínio: filha nova numa edição precisa entrar como inserção, não como alteração.
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Description).HasMaxLength(Proposal.MaxDescriptionLength);
        builder.Property(i => i.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Quantity).HasPrecision(12, 2);
        builder.Property(i => i.UnitAmount).HasPrecision(19, 4);
        builder.Property(i => i.Percentage).HasPrecision(5, 2);
        builder.Ignore(i => i.Amount);
        builder.HasIndex(i => i.ProposalId);
    }

    public void Configure(EntityTypeBuilder<ProposalTemplate> builder)
    {
        builder.ToTable("proposal_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(ProposalTemplate.MaxNameLength);
        builder.Property(t => t.FileName).HasMaxLength(255);
        builder.Property(t => t.Sha256).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(t => new { t.OrganizationId, t.Name });
        builder.HasStandardColumns();
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c>; nunca conecta ao banco.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql("Host=localhost;Database=lexflow_design", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CrmDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? OrganizationId => null;
    }
}

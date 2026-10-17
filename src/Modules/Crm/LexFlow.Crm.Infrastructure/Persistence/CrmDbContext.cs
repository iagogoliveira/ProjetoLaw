using LexFlow.Application.Abstractions;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain.People;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Crm.Infrastructure.Persistence;

/// <summary>DbContext do módulo Crm (schema <c>crm</c>): pessoas, papéis, endereços, contatos e grupos econômicos.</summary>
public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenant)
    : ModuleDbContextBase(options, tenant), ICrmDbContext
{
    public const string SchemaName = "crm";
    public const string ModuleName = "Crm";

    public DbSet<Person> People => Set<Person>();

    public DbSet<EconomicGroup> EconomicGroups => Set<EconomicGroup>();

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

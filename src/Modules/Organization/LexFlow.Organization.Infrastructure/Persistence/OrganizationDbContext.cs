using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.Organization.Application.Abstractions;
using LexFlow.Organization.Domain.Members;
using LexFlow.Organization.Domain.OrgStructure;
using LexFlow.Organization.Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrganizationEntity = LexFlow.Organization.Domain.Organizations.Organization;

namespace LexFlow.Organization.Infrastructure.Persistence;

/// <summary>DbContext do módulo Organization (schema <c>org</c>): organizações, membros, estrutura e perfis.</summary>
public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options, ITenantContext tenant)
    : ModuleDbContextBase(options, tenant), IOrganizationDbContext
{
    public const string SchemaName = "org";
    public const string ModuleName = "Organization";

    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();

    public DbSet<OrganizationMember> Members => Set<OrganizationMember>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<MemberRoleAssignment> RoleAssignments => Set<MemberRoleAssignment>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<PracticeArea> PracticeAreas => Set<PracticeArea>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    public DbSet<Partner> Partners => Set<Partner>();

    protected override string Schema => SchemaName;
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<OrganizationEntity>
{
    public void Configure(EntityTypeBuilder<OrganizationEntity> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Name).HasMaxLength(OrganizationEntity.MaxNameLength);
        builder.HasStandardColumns();
    }
}

/// <summary>Um usuário entra uma vez em cada organização (índice parcial: ignora as pertenças excluídas).</summary>
internal sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("members");
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique().HasFilter("NOT is_deleted");
        builder.HasIndex(m => m.UserId);
        builder.HasOne<OrganizationEntity>().WithMany().HasForeignKey(m => m.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasStandardColumns();
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(Role.MaxNameLength);
        builder.Property(r => r.Description).HasMaxLength(Role.MaxDescriptionLength);
        builder.Property(r => r.SystemKey).HasMaxLength(50);
        builder.HasIndex(r => new { r.OrganizationId, r.Name }).IsUnique().HasFilter("NOT is_deleted");
        builder.HasIndex(r => new { r.OrganizationId, r.SystemKey }).IsUnique().HasFilter("system_key IS NOT NULL AND NOT is_deleted");
        builder.HasOne<OrganizationEntity>().WithMany().HasForeignKey(r => r.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(r => r.Permissions).WithOne().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasStandardColumns();
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(p => new { p.RoleId, p.Permission });
        builder.Property(p => p.Permission).HasMaxLength(RolePermission.MaxPermissionLength);
        builder.HasIndex(p => new { p.OrganizationId, p.Permission });
    }
}

internal sealed class MemberRoleAssignmentConfiguration : IEntityTypeConfiguration<MemberRoleAssignment>
{
    public void Configure(EntityTypeBuilder<MemberRoleAssignment> builder)
    {
        builder.ToTable("member_role_assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Scope).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(a => new { a.OrganizationId, a.MemberId });
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(a => a.MemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(a => a.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasStandardColumns();
    }
}

internal sealed class StructureConfiguration :
    IEntityTypeConfiguration<Branch>,
    IEntityTypeConfiguration<PracticeArea>,
    IEntityTypeConfiguration<Team>,
    IEntityTypeConfiguration<TeamMember>,
    IEntityTypeConfiguration<CostCenter>,
    IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        Named(builder, "branches");
        builder.Property(b => b.Cnpj).HasMaxLength(14);
        builder.Property(b => b.MunicipalRegistration).HasMaxLength(30);
    }

    public void Configure(EntityTypeBuilder<PracticeArea> builder) => Named(builder, "practice_areas");

    public void Configure(EntityTypeBuilder<Team> builder)
    {
        Named(builder, "teams");
        builder.HasOne<PracticeArea>().WithMany().HasForeignKey(t => t.PracticeAreaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("team_members");
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => new { m.OrganizationId, m.TeamId, m.MemberId }).IsUnique();
        builder.HasOne<Team>().WithMany().HasForeignKey(m => m.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(m => m.MemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<CostCenter> builder)
    {
        builder.ToTable("cost_centers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(StructureEntity.MaxNameLength);
        builder.Property(c => c.Code).HasMaxLength(CostCenter.MaxCodeLength);
        builder.HasIndex(c => new { c.OrganizationId, c.Code }).IsUnique().HasFilter("NOT is_deleted");
        builder.HasStandardColumns();
    }

    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("partners");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(StructureEntity.MaxNameLength);
        builder.Property(p => p.SharePercentage).HasPrecision(7, 4);
        builder.HasIndex(p => p.OrganizationId);
        builder.HasOne<OrganizationMember>().WithMany().HasForeignKey(p => p.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasStandardColumns();
    }

    /// <summary>Tabela com nome único dentro do tenant (índice parcial, seção 6.1).</summary>
    private static void Named<T>(EntityTypeBuilder<T> builder, string table)
        where T : StructureEntity
    {
        builder.ToTable(table);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(StructureEntity.MaxNameLength);
        builder.HasIndex(e => new { e.OrganizationId, e.Name }).IsUnique().HasFilter("NOT is_deleted");
        builder.HasStandardColumns();
    }
}

/// <summary>Usado só pelo <c>dotnet ef migrations add</c>; nunca conecta ao banco.</summary>
internal sealed class DesignTimeFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql("Host=localhost;Database=lexflow_design", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", OrganizationDbContext.SchemaName))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new OrganizationDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? OrganizationId => null;
    }
}

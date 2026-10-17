using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Data;
using LexFlow.Identity.Application.Abstractions;
using LexFlow.Identity.Domain.Sessions;
using LexFlow.Identity.Domain.Users;
using LexFlow.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

namespace LexFlow.Identity.Infrastructure.Persistence;

/// <summary>
/// DbContext do módulo Identity (schema <c>identity</c>): tabelas do ASP.NET Core Identity sem papéis globais (os perfis
/// são por organização, Etapa 3), sessões, a tabela do anel de chaves do Data Protection (lida e gravada pelo
/// <see cref="DataProtectionKeysDbContext"/>) e as convenções comuns de módulo (auditoria, outbox).
/// </summary>
public sealed class IdentityModuleDbContext(DbContextOptions<IdentityModuleDbContext> options, ITenantContext tenant)
    : IdentityUserContext<ApplicationUser, Guid>(options), IModuleDbContext, IIdentityDbContext
{
    public const string SchemaName = "identity";
    public const string ModuleName = "Identity";

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public Guid? CurrentOrganizationId => tenant.OrganizationId;

    public bool HasActiveTransaction => Database.CurrentTransaction is not null;

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        ModuleModel.BeginTransactionAsync(Database, cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Passkeys ficam para a Etapa 16 (WebAuthn, seção 7.1); sem isso o modelo varia com a versão de schema do Identity.
        builder.Ignore<IdentityUserPasskey<Guid>>();
        builder.Ignore<IdentityPasskeyData>();

        // Aplicações, autorizações, escopos e tokens do servidor OIDC (seção 7.1), com chave Guid.
        builder.UseOpenIddict<Guid>();

        ModuleModel.Apply(builder, this, SchemaName, ownsAuditLog: false, typeof(IdentityModuleDbContext).Assembly);
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");
        builder.Property(u => u.FullName).HasMaxLength(ApplicationUser.MaxFullNameLength);
        builder.Property(u => u.MfaMethod).HasConversion<string>().HasMaxLength(20).HasDefaultValue(MfaMethod.None);

        // Nomes no padrão snake_case (seção 6.1). O e-mail também é o login: único no banco, não só na aplicação.
        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_users_normalized_user_name");
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_users_normalized_email").IsUnique();

        // Segredos nunca entram na trilha de auditoria (seção 6.4).
        builder.Property(u => u.PasswordHash).IsSensitive();
        builder.Property(u => u.SecurityStamp).IsSensitive();
        builder.Property(u => u.ConcurrencyStamp).IsSensitive();
    }
}

internal sealed class IdentityTablesConfiguration :
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("user_claims");

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("user_logins");

    /// <summary>Chave do autenticador, códigos de recuperação e tokens: fora da trilha.</summary>
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens");
        builder.IsNotAudited();
    }
}

/// <summary>Só para as migrations: o acesso às chaves é do <see cref="DataProtectionKeysDbContext"/>. Fora da auditoria.</summary>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("data_protection_keys");
        builder.IsNotAudited();
    }
}

/// <summary>
/// Tabelas do OpenIddict em snake_case. Autorizações e tokens ficam fora da trilha (volume e conteúdo secreto); o
/// que importa para auditoria é o início e o fim da sessão, gravados em <c>sessions</c>.
/// </summary>
internal sealed class OpenIddictTablesConfiguration :
    IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreApplication<Guid>>,
    IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreAuthorization<Guid>>,
    IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreScope<Guid>>,
    IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreToken<Guid>>
{
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreApplication<Guid>> builder)
    {
        builder.ToTable("openiddict_applications");
        builder.Property(a => a.ClientSecret).IsSensitive();
        builder.Property(a => a.JsonWebKeySet).IsSensitive();
    }

    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreAuthorization<Guid>> builder)
    {
        builder.ToTable("openiddict_authorizations");
        builder.IsNotAudited();
    }

    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreScope<Guid>> builder) => builder.ToTable("openiddict_scopes");

    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreToken<Guid>> builder)
    {
        builder.ToTable("openiddict_tokens");
        builder.IsNotAudited();
    }
}

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.AuthorizationId).HasMaxLength(100);
        builder.Property(s => s.IpAddress).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(Session.MaxUserAgentLength);
        builder.Property(s => s.RevokedReason).HasMaxLength(100);
        builder.Ignore(s => s.ExpiresAt);
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.AuthorizationId);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

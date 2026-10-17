using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Organization.Domain.Roles;

/// <summary>Escopo de dados de uma atribuição de perfil (seção 7.2).</summary>
public enum DataScope
{
    /// <summary>Só o que é do próprio usuário (responsável, autor).</summary>
    Own,

    Team,
    PracticeArea,
    Branch,
    Organization,
}

/// <summary>
/// Perfil (seção 7.2): pertence à organização e agrega permissões <c>modulo.recurso.acao</c>. Os perfis padrão da
/// seção 3.2 nascem no onboarding com <see cref="SystemKey"/> e continuam editáveis pelo escritório. Quem confere
/// que cada permissão existe no catálogo é a Application.
/// </summary>
public sealed class Role : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int MaxNameLength = 100;
    public const int MaxDescriptionLength = 500;

    private readonly List<RolePermission> _permissions = [];

    private Role()
    {
    }

    private Role(Guid id, string name, string? description, string? systemKey)
        : base(id)
    {
        Name = name;
        Description = description;
        SystemKey = systemKey;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>Identifica um perfil padrão (ex.: <c>office_admin</c>); nulo nos perfis criados pelo escritório.</summary>
    public string? SystemKey { get; private set; }

    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Role Create(Guid id, string name, string? description = null, string? systemKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Role(id, name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(), systemKey);
    }

    /// <summary>Substitui o conjunto de permissões (a tela de perfis envia a matriz inteira).</summary>
    public void SetPermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var wanted = permissions.Select(p => p.Trim()).Where(p => p.Length > 0).ToHashSet(StringComparer.Ordinal);
        _permissions.RemoveAll(p => !wanted.Contains(p.Permission));
        foreach (var permission in wanted.Where(p => _permissions.TrueForAll(existing => !string.Equals(existing.Permission, p, StringComparison.Ordinal))).Order(StringComparer.Ordinal))
        {
            _permissions.Add(new RolePermission(Id, permission));
        }
    }

    public bool Grants(string permission) => _permissions.Exists(p => string.Equals(p.Permission, permission, StringComparison.Ordinal));
}

/// <summary>Permissão concedida por um perfil.</summary>
#pragma warning disable CA1711 // nome da seção 9 (Etapa 3, tarefa 1): é a linha "perfil × permissão", não um tipo de permissão .NET
public sealed class RolePermission : ITenantOwned
#pragma warning restore CA1711
{
    public const int MaxPermissionLength = 100;

    private RolePermission()
    {
    }

    internal RolePermission(Guid roleId, string permission)
    {
        RoleId = roleId;
        Permission = permission;
    }

    public Guid OrganizationId { get; private set; }

    public Guid RoleId { get; private set; }

    public string Permission { get; private set; } = string.Empty;
}

/// <summary>Atribuição de perfil a um membro, com escopo de dados (seção 7.2: <c>RoleId, DataScope, ScopeRefId</c>).</summary>
public sealed class MemberRoleAssignment : Entity<Guid>, ITenantOwned, IAuditable
{
    private MemberRoleAssignment()
    {
    }

    private MemberRoleAssignment(Guid id, Guid memberId, Guid roleId, DataScope scope, Guid? scopeRefId)
        : base(id)
    {
        MemberId = memberId;
        RoleId = roleId;
        Scope = scope;
        ScopeRefId = scopeRefId;
    }

    public Guid OrganizationId { get; private set; }

    public Guid MemberId { get; private set; }

    public Guid RoleId { get; private set; }

    public DataScope Scope { get; private set; }

    /// <summary>Equipe, área ou filial do escopo; nulo em <see cref="DataScope.Own"/> e <see cref="DataScope.Organization"/>.</summary>
    public Guid? ScopeRefId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Result<MemberRoleAssignment> Assign(Guid id, Guid memberId, Guid roleId, DataScope scope, Guid? scopeRefId = null)
    {
        var needsReference = scope is DataScope.Team or DataScope.PracticeArea or DataScope.Branch;
        if (needsReference && scopeRefId is null)
        {
            return OrganizationErrors.ScopeReferenceRequired;
        }

        if (!needsReference && scopeRefId is not null)
        {
            return OrganizationErrors.ScopeReferenceNotAllowed;
        }

        return new MemberRoleAssignment(id, memberId, roleId, scope, scopeRefId);
    }
}

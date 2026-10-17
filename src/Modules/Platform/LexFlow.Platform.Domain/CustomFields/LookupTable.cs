using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Platform.Domain.CustomFields;

/// <summary>
/// Tabela auxiliar editável do escritório (RF-03): tipos de ação, fases, objetos/pedidos etc. As do sistema nascem no
/// onboarding com <see cref="SystemKey"/> e itens sugeridos; o escritório cria outras para usar em campos lista.
/// Tabelas e itens não são excluídos (os valores gravados apontam para eles): são desativados.
/// </summary>
public sealed class LookupTable : AggregateRoot<Guid>, ITenantOwned, IAuditable
{
    public const int MaxNameLength = 100;

    private LookupTable()
    {
    }

    private LookupTable(Guid id, string name, string? systemKey)
        : base(id)
    {
        Name = name;
        SystemKey = systemKey;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Identifica uma tabela do sistema (ex.: <c>action_types</c>), usada pelos módulos; nula nas do escritório.</summary>
    public string? SystemKey { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static LookupTable Create(Guid id, string name, string? systemKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new LookupTable(id, name.Trim(), systemKey);
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>Tabela do sistema fica sempre ativa: os módulos dependem dela.</summary>
    public Result Activate(bool active)
    {
        if (!active && SystemKey is not null)
        {
            return CustomFieldErrors.SystemTableLocked;
        }

        IsActive = active;
        return Result.Success;
    }
}

/// <summary>Opção de uma tabela auxiliar.</summary>
public sealed class LookupItem : Entity<Guid>, ITenantOwned, IAuditable
{
    public const int MaxNameLength = 200;

    private LookupItem()
    {
    }

    private LookupItem(Guid id, Guid tableId, string name, int position)
        : base(id)
    {
        TableId = tableId;
        Name = name;
        Position = position;
    }

    public Guid OrganizationId { get; private set; }

    public Guid TableId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Position { get; private set; }

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static LookupItem Create(Guid id, Guid tableId, string name, int position)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new LookupItem(id, tableId, name.Trim(), position);
    }

    public void Update(string name, int position, bool active)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Position = position;
        IsActive = active;
    }
}

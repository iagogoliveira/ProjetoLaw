using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Organization.Domain.OrgStructure;

/// <summary>Base das entidades de estrutura (seção 3.1): dado de escritório com nome, auditoria e exclusão lógica.</summary>
public abstract class StructureEntity : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int MaxNameLength = 200;

    protected StructureEntity()
    {
    }

    protected StructureEntity(Guid id, string name)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }
}

/// <summary>Filial/unidade, com dados fiscais próprios para faturamento.</summary>
public sealed class Branch : StructureEntity
{
    private Branch()
    {
    }

    private Branch(Guid id, string name, Cnpj? cnpj, string? municipalRegistration)
        : base(id, name)
    {
        Cnpj = cnpj?.Value;
        MunicipalRegistration = string.IsNullOrWhiteSpace(municipalRegistration) ? null : municipalRegistration.Trim();
    }

    /// <summary>Só dígitos (ou alfanumérico, a partir de 2026), validado pelo value object <see cref="SharedKernel.ValueObjects.Cnpj"/>.</summary>
    public string? Cnpj { get; private set; }

    public string? MunicipalRegistration { get; private set; }

    public static Branch Create(Guid id, string name, Cnpj? cnpj = null, string? municipalRegistration = null) =>
        new(id, name, cnpj, municipalRegistration);

    public void Update(string name, Cnpj? cnpj, string? municipalRegistration)
    {
        Rename(name);
        Cnpj = cnpj?.Value;
        MunicipalRegistration = string.IsNullOrWhiteSpace(municipalRegistration) ? null : municipalRegistration.Trim();
    }
}

/// <summary>Área de atuação ou núcleo (trabalhista, cível, tributário…).</summary>
public sealed class PracticeArea : StructureEntity
{
    private PracticeArea()
    {
    }

    private PracticeArea(Guid id, string name)
        : base(id, name)
    {
    }

    public static PracticeArea Create(Guid id, string name) => new(id, name);
}

/// <summary>Equipe: grupo que compartilha carteira e fila de trabalho; base do escopo de dados <c>Team</c>.</summary>
public sealed class Team : StructureEntity
{
    private Team()
    {
    }

    private Team(Guid id, string name, Guid? practiceAreaId, Guid? branchId)
        : base(id, name)
    {
        PracticeAreaId = practiceAreaId;
        BranchId = branchId;
    }

    public Guid? PracticeAreaId { get; private set; }

    public Guid? BranchId { get; private set; }

    public static Team Create(Guid id, string name, Guid? practiceAreaId = null, Guid? branchId = null) =>
        new(id, name, practiceAreaId, branchId);

    public void Update(string name, Guid? practiceAreaId, Guid? branchId)
    {
        Rename(name);
        PracticeAreaId = practiceAreaId;
        BranchId = branchId;
    }
}

/// <summary>Membro de uma equipe.</summary>
public sealed class TeamMember : Entity<Guid>, ITenantOwned, IAuditable
{
    private TeamMember()
    {
    }

    private TeamMember(Guid id, Guid teamId, Guid memberId)
        : base(id)
    {
        TeamId = teamId;
        MemberId = memberId;
    }

    public Guid OrganizationId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static TeamMember Add(Guid id, Guid teamId, Guid memberId) => new(id, teamId, memberId);
}

/// <summary>Centro de custo para rateio de despesas, timesheet e rentabilidade.</summary>
public sealed class CostCenter : StructureEntity
{
    public const int MaxCodeLength = 30;

    private CostCenter()
    {
    }

    private CostCenter(Guid id, string code, string name)
        : base(id, name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = NormalizeCode(code);
    }

    public string Code { get; private set; } = string.Empty;

    public static CostCenter Create(Guid id, string code, string name) => new(id, code, name);

    /// <summary>Código normalizado como gravado (sem espaços nas pontas, em maiúsculas).</summary>
    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public void Update(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Rename(name);
        Code = NormalizeCode(code);
    }
}

/// <summary>Sócio e participação, para relatórios de distribuição de resultado.</summary>
public sealed class Partner : StructureEntity
{
    private Partner()
    {
    }

    private Partner(Guid id, string name, Guid? memberId, decimal sharePercentage)
        : base(id, name)
    {
        MemberId = memberId;
        SharePercentage = sharePercentage;
    }

    /// <summary>Membro do escritório, quando o sócio também usa o sistema.</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>Participação em % (ex.: 33.3333), maior que 0 e até 100.</summary>
    public decimal SharePercentage { get; private set; }

    public static Result<Partner> Create(Guid id, string name, decimal sharePercentage, Guid? memberId = null) =>
        IsValidShare(sharePercentage)
            ? new Partner(id, name, memberId, sharePercentage)
            : OrganizationErrors.InvalidPartnerShare;

    public Result Update(string name, decimal sharePercentage, Guid? memberId)
    {
        if (!IsValidShare(sharePercentage))
        {
            return OrganizationErrors.InvalidPartnerShare;
        }

        Rename(name);
        SharePercentage = sharePercentage;
        MemberId = memberId;
        return Result.Success;
    }

    private static bool IsValidShare(decimal sharePercentage) => sharePercentage is > 0 and <= 100;
}

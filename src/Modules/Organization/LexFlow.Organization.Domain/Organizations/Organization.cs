using LexFlow.SharedKernel.Domain;

namespace LexFlow.Organization.Domain.Organizations;

/// <summary>
/// Organização (tenant, seção 3.1): o escritório ou a empresa contratante. O próprio <see cref="Entity{TId}.Id"/> é o
/// <c>OrganizationId</c> dos dados de escritório, por isso a entidade não é <see cref="ITenantOwned"/>.
/// </summary>
public sealed class Organization : AggregateRoot<Guid>, IAuditable
{
    public const int MaxNameLength = 200;

    private Organization()
    {
    }

    private Organization(Guid id, string name)
        : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Exige segundo fator de todos os membros (RF-01; aplicado no PR 3B).</summary>
    public bool MfaRequired { get; private set; }

    /// <summary>Exige conferência independente antes de marcar o prazo como cumprido; não bloqueia o início do trabalho.</summary>
    public bool RequireDeadlineReview { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Organization Create(Guid id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Organization(id, name.Trim());
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void RequireMfa(bool required) => MfaRequired = required;

    public void SetDeadlineReviewRequired(bool required) => RequireDeadlineReview = required;
}

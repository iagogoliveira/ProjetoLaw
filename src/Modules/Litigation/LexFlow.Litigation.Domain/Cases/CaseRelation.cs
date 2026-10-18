using LexFlow.SharedKernel.Domain;

namespace LexFlow.Litigation.Domain.Cases;

/// <summary>Como o processo vinculado se relaciona com o principal (RF-09: hierarquia de processos).</summary>
public enum RelationKind
{
    Incident,
    Appeal,
    Enforcement,
    Attached,
}

/// <summary>
/// Vínculo na hierarquia: o processo <see cref="ChildCaseId"/> é incidente, recurso, cumprimento de sentença ou
/// apenso do principal <see cref="ParentCaseId"/>. Um processo tem no máximo um principal, e a hierarquia não tem
/// ciclos (conferidos no caso de uso).
/// </summary>
public sealed class CaseRelation : Entity<Guid>, ITenantOwned, IAuditable
{
    private CaseRelation()
    {
    }

    private CaseRelation(Guid id, Guid parentCaseId, Guid childCaseId, RelationKind kind)
        : base(id)
    {
        ParentCaseId = parentCaseId;
        ChildCaseId = childCaseId;
        Kind = kind;
    }

    public Guid OrganizationId { get; private set; }

    public Guid ParentCaseId { get; private set; }

    public Guid ChildCaseId { get; private set; }

    public RelationKind Kind { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static CaseRelation Link(Guid id, Guid parentCaseId, Guid childCaseId, RelationKind kind) =>
        new(id, parentCaseId, childCaseId, kind);
}

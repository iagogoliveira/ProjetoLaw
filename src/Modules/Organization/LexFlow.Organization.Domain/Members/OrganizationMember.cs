using LexFlow.SharedKernel.Domain;

namespace LexFlow.Organization.Domain.Members;

/// <summary>
/// Pertença de um usuário (global, módulo Identity) a uma organização (RF-02: a mesma pessoa pode estar em vários
/// escritórios). É contra esta tabela que o tenant da requisição é validado (seção 6.2, item 2).
/// </summary>
public sealed class OrganizationMember : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    private OrganizationMember()
    {
    }

    private OrganizationMember(Guid id, Guid organizationId, Guid userId, DateTimeOffset joinedAt)
        : base(id)
    {
        OrganizationId = organizationId;
        UserId = userId;
        JoinedAt = joinedAt;
    }

    public Guid OrganizationId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static OrganizationMember Join(Guid id, Guid organizationId, Guid userId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Usuário obrigatório.", nameof(userId));
        }

        return new OrganizationMember(id, organizationId, userId, now);
    }
}

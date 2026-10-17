using LexFlow.Organization.Domain.Roles;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;
using EmailAddress = LexFlow.SharedKernel.ValueObjects.Email;

namespace LexFlow.Organization.Domain.Invitations;

/// <summary>Situação gravada de um convite. "Expirado" não é gravado: é um pendente com o prazo vencido.</summary>
public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked,
}

/// <summary>
/// Convite por e-mail para entrar na organização (RF-01, Etapa 3, tarefa 8), com os perfis que o convidado recebe ao
/// aceitar. Vale 7 dias a partir do envio (ou do reenvio). O token não fica aqui: o Worker o gera na hora do envio e
/// grava só o hash no diretório <see cref="InvitationToken"/>.
/// </summary>
public sealed class Invitation : AggregateRoot<Guid>, ITenantOwned, IAuditable
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private readonly List<InvitationRole> _roles = [];

    private Invitation()
    {
    }

    private Invitation(Guid id, string email, Guid invitedBy, DateTimeOffset expiresAt)
        : base(id)
    {
        Email = email;
        InvitedBy = invitedBy;
        ExpiresAt = expiresAt;
    }

    public Guid OrganizationId { get; private set; }

    /// <summary>E-mail convidado, normalizado; só a conta com este e-mail confirmado aceita.</summary>
    public string Email { get; private set; } = string.Empty;

    public Guid InvitedBy { get; private set; }

    public InvitationStatus Status { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>Membro criado no aceite.</summary>
    public Guid? MemberId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public IReadOnlyCollection<InvitationRole> Roles => _roles.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public static Result<Invitation> Create(Guid id, string email, Guid invitedBy, IReadOnlyCollection<MemberRoleAssignment> roles, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(roles);
        var address = EmailAddress.Create(email);
        if (address.IsFailure)
        {
            return OrganizationErrors.InvalidEmail;
        }

        if (roles.Count == 0)
        {
            return OrganizationErrors.InvitationRolesRequired;
        }

        var invitation = new Invitation(id, address.Value.Value, invitedBy, now + Lifetime);
        invitation._roles.AddRange(roles.Select(r => new InvitationRole(r.Id, id, r.RoleId, r.Scope, r.ScopeRefId)));
        return invitation;
    }

    public bool IsExpired(DateTimeOffset now) => Status == InvitationStatus.Pending && now >= ExpiresAt;

    /// <summary>Pendente e dentro do prazo: só assim o link enviado vale.</summary>
    public bool IsOpen(DateTimeOffset now) => Status == InvitationStatus.Pending && now < ExpiresAt;

    /// <summary>Reenvio: o prazo recomeça e o Worker gera um token novo (o anterior deixa de valer).</summary>
    public Result Renew(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return OrganizationErrors.InvitationNotPending;
        }

        ExpiresAt = now + Lifetime;
        return Result.Success;
    }

    public Result Revoke(DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return OrganizationErrors.InvitationNotPending;
        }

        Status = InvitationStatus.Revoked;
        RevokedAt = now;
        return Result.Success;
    }

    public Result Accept(Guid memberId, DateTimeOffset now)
    {
        if (Status != InvitationStatus.Pending)
        {
            return OrganizationErrors.InvitationNotFound;
        }

        if (IsExpired(now))
        {
            return OrganizationErrors.InvitationExpired;
        }

        Status = InvitationStatus.Accepted;
        AcceptedAt = now;
        MemberId = memberId;
        return Result.Success;
    }
}

/// <summary>Perfil (com escopo de dados) que o convidado recebe ao aceitar.</summary>
public sealed class InvitationRole : ITenantOwned
{
    private InvitationRole()
    {
    }

    internal InvitationRole(Guid id, Guid invitationId, Guid roleId, DataScope scope, Guid? scopeRefId)
    {
        Id = id;
        InvitationId = invitationId;
        RoleId = roleId;
        Scope = scope;
        ScopeRefId = scopeRefId;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid InvitationId { get; private set; }

    public Guid RoleId { get; private set; }

    public DataScope Scope { get; private set; }

    public Guid? ScopeRefId { get; private set; }
}

/// <summary>
/// Diretório token → convite (dado da plataforma, sem tenant, como o <c>UserOrganization</c>): quem abre o link ainda
/// não é membro, então o convite é achado pelo hash antes de haver organização ativa. Guarda só o SHA-256 do token;
/// há no máximo um por convite e ele some no aceite, na revogação ou quando o Worker gera outro.
/// </summary>
[NotAudited]
public sealed class InvitationToken
{
    public const int HashLength = 64;

    private InvitationToken()
    {
    }

    public InvitationToken(string tokenHash, Guid invitationId, Guid organizationId, string email, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        TokenHash = tokenHash;
        InvitationId = invitationId;
        OrganizationId = organizationId;
        Email = email;
        ExpiresAt = expiresAt;
    }

    /// <summary>SHA-256 do token, em hexadecimal minúsculo.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public Guid InvitationId { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }
}

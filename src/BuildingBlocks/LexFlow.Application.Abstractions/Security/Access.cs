using LexFlow.SharedKernel.Results;

namespace LexFlow.Application.Abstractions.Security;

/// <summary>Escopo de dados de uma permissão concedida (seção 7.2).</summary>
public enum AccessScope
{
    Own,
    Team,
    PracticeArea,
    Branch,
    Organization,
}

/// <summary>Escopo concedido por uma atribuição de perfil; <see cref="ReferenceId"/> é a equipe, área ou filial.</summary>
public sealed record ScopeGrant(AccessScope Scope, Guid? ReferenceId);

/// <summary>Organizações das quais o usuário é membro e a última escolhida por ele (troca de organização).</summary>
public sealed record MembershipSnapshot(IReadOnlyList<Guid> OrganizationIds, Guid? PreferredOrganizationId)
{
    public static MembershipSnapshot None { get; } = new([], null);

    /// <summary>Sem pedido explícito: a preferida, se ainda for membro; senão a única; senão nenhuma.</summary>
    public Guid? DefaultOrganization =>
        PreferredOrganizationId is { } preferred && OrganizationIds.Contains(preferred)
            ? preferred
            : OrganizationIds.Count == 1 ? OrganizationIds[0] : null;
}

/// <summary>Pertença do usuário às organizações (seção 6.2, item 2), com cache; implementada pelo módulo Organization.</summary>
public interface IMembershipLookup
{
    Task<MembershipSnapshot> GetAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Quantos membros ativos tem a organização ativa (implementado pelo módulo Organization). Usado em regras que dependem
/// do tamanho do escritório, como a aprovação da própria proposta quando só há um membro (ADR 0024).
/// </summary>
public interface IOrganizationMembers
{
    Task<int> CountActiveAsync(CancellationToken cancellationToken);
}

public enum PermissionDecision
{
    Allowed,
    Denied,

    /// <summary>A organização exige segundo fator e o token não traz <c>amr=mfa</c>.</summary>
    MfaRequired,
}

public sealed record PermissionCheck(Guid UserId, Guid? OrganizationId, string Permission, bool MultiFactorAuthenticated);

/// <summary>Primeira etapa da autorização (seção 7.2): a permissão do endpoint, pelo perfil do membro na organização ativa.</summary>
public interface IPermissionChecker
{
    Task<PermissionDecision> CheckAsync(PermissionCheck check, CancellationToken cancellationToken);
}

/// <summary>
/// Permissões do usuário na organização ativa, para a segunda etapa (autorizadores de recurso): em que escopos ele
/// tem cada permissão. Lista vazia = não tem.
/// </summary>
public interface ICurrentAccess
{
    /// <summary>Membro da organização ativa; nulo sem tenant ou sem pertença.</summary>
    Task<Guid?> MemberIdAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ScopeGrant>> ScopesForAsync(string permission, CancellationToken cancellationToken);
}

/// <summary>
/// Segunda etapa da autorização (seção 7.2): o recurso pedido está no escopo do usuário (responsável, equipe, área,
/// filial) e não é um caso sigiloso fora da lista? Implementações ficam no Application do módulo, uma por request.
/// Roda depois da validação e antes da transação; recurso de outra organização continua dando 404 pelo filtro.
/// </summary>
public interface IResourceAuthorizer<in TRequest>
{
    Task<Result> AuthorizeAsync(TRequest request, CancellationToken cancellationToken);
}

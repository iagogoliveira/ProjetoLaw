namespace LexFlow.Organization.Contracts.OrgStructure;

/// <summary>Cadastros da estrutura do escritório que os outros módulos referenciam.</summary>
public enum OrgUnitKind
{
    PracticeArea,
    Branch,
    Team,
}

/// <summary>Um cadastro da estrutura: o id e o nome.</summary>
public sealed record OrgUnit(Guid Id, OrgUnitKind Kind, string Name);

/// <summary>
/// A estrutura da organização ativa (áreas, filiais, equipes) para os outros módulos (ex.: o processo aponta para a
/// área e a filial). Implementado pelo Organization.
/// </summary>
public interface IOrganizationDirectory
{
    /// <summary>Os cadastros do tipo pedido com os ids informados que existem no escritório.</summary>
    Task<IReadOnlyList<OrgUnit>> GetAsync(OrgUnitKind kind, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Todos os cadastros do tipo pedido no escritório (ex.: para achar a área pelo nome numa importação).</summary>
    Task<IReadOnlyList<OrgUnit>> ListAsync(OrgUnitKind kind, CancellationToken cancellationToken);

    /// <summary>Os usuários membros do escritório.</summary>
    Task<IReadOnlyList<Guid>> MemberUserIdsAsync(CancellationToken cancellationToken);

    /// <summary>As equipes de que o usuário é membro no escritório.</summary>
    Task<IReadOnlyList<Guid>> TeamsOfUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Os usuários membros das equipes informadas.</summary>
    Task<IReadOnlyList<Guid>> UsersOfTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken);
}

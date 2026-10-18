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
}

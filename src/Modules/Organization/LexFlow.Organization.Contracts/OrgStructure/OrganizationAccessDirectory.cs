namespace LexFlow.Organization.Contracts.OrgStructure;

public enum OfficeAccessScope { Own, Team, PracticeArea, Branch, Organization }
public sealed record OfficeScope(OfficeAccessScope Scope, Guid? ReferenceId);
public sealed record OfficeUserAccess(bool MfaRequired, IReadOnlyDictionary<string, IReadOnlyList<OfficeScope>> Permissions);
/// <summary>Permissões por usuário no tenant ativo e IDs globais dos escritórios para o dispatcher interno.</summary>
public interface IOrganizationAccessDirectory
{
    Task<OfficeUserAccess?> GetAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> UserIdsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> OfficeIdsAsync(CancellationToken cancellationToken);
}

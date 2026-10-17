using LexFlow.Application.Abstractions.Security;
using LexFlow.Organization.Application.Access;

namespace LexFlow.Organization.Application.Abstractions;

/// <summary>Cache das permissões (5 min) e das pertenças de cada usuário, com invalidação explícita.</summary>
public interface IAccessCache
{
    /// <summary>Permissões do usuário na organização; <paramref name="load"/> roda só quando não há cache válido.</summary>
    Task<AccessSnapshot?> GetOrLoadAsync(Guid organizationId, Guid userId, Func<Task<AccessSnapshot?>> load, CancellationToken cancellationToken);

    /// <summary>Descarta as permissões de todos os membros da organização.</summary>
    Task InvalidateOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<MembershipSnapshot?> GetMembershipsAsync(Guid userId, CancellationToken cancellationToken);

    Task SetMembershipsAsync(Guid userId, MembershipSnapshot memberships, CancellationToken cancellationToken);

    Task InvalidateMembershipsAsync(Guid userId, CancellationToken cancellationToken);
}

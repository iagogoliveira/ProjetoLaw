namespace LexFlow.Identity.Contracts;

/// <summary>Dados públicos de um usuário para outros módulos (nome e e-mail nas telas de membros e nos convites).</summary>
public sealed record UserSummary(Guid Id, string Email, string FullName, bool EmailConfirmed);

/// <summary>
/// Consulta síncrona de usuários pelos outros módulos (seção 4.2: módulos só se conhecem pelos Contracts). Usuários são
/// globais, anteriores a qualquer escritório; o diretório não expõe senha, segundo fator nem sessões.
/// </summary>
public interface IUserDirectory
{
    Task<UserSummary?> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Busca pelo e-mail normalizado (minúsculas, sem espaços).</summary>
    Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSummary>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

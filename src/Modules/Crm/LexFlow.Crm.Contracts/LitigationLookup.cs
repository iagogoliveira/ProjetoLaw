namespace LexFlow.Crm.Contracts;

/// <summary>Parte de um processo encontrada na consulta, com o lado dela em relação ao cliente do escritório.</summary>
public sealed record CasePartyMatch(Guid CaseId, string CaseNumber, string PartyName, string? PartyDocument, CasePartySide Side, string ClientName);

public enum CasePartySide
{
    Client,
    Opponent,
}

/// <summary>
/// Partes dos processos do escritório, para o conflito de interesses (Etapa 4, tarefa 3). O Crm consome; o módulo de
/// processos (Litigation, Etapa 5) implementa. Sem ele registrado, o Crm usa uma implementação que não encontra nada.
/// </summary>
public interface ILitigationLookup
{
    /// <summary>Partes com o nome normalizado (<c>SearchText</c>) ou o documento (só dígitos) informados.</summary>
    Task<IReadOnlyList<CasePartyMatch>> FindPartiesAsync(string searchName, string? document, CancellationToken cancellationToken);
}

/// <summary>Dados de uma pessoa do cadastro, como os outros módulos precisam (nome, documento e situação).</summary>
public sealed record PersonCard(Guid Id, string Kind, string Name, string? Document, bool IsAnonymized);

/// <summary>
/// Consulta ao cadastro de pessoas do Crm pelos outros módulos (ex.: partes dos processos), sempre na organização
/// ativa. Implementado pelo Crm.
/// </summary>
public interface IPeopleDirectory
{
    /// <summary>As pessoas com os ids pedidos que existem no escritório (as excluídas não voltam).</summary>
    Task<IReadOnlyList<PersonCard>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Ids das pessoas com o nome normalizado (<c>SearchText</c>) ou o documento (só dígitos) informados.</summary>
    Task<IReadOnlyList<Guid>> FindAsync(string searchName, string? document, CancellationToken cancellationToken);
}

/// <summary>Papel com que a pessoa entra pela importação de processos.</summary>
public enum PersonSeedRole
{
    Client,
    Opponent,
}

/// <summary>Pessoa citada numa linha importada: o nome e, se houver, o CPF ou CNPJ (com ou sem pontuação).</summary>
public sealed record PersonSeed(string Name, string? Document, PersonSeedRole Role);

/// <summary>
/// Acha ou cadastra as pessoas citadas numa importação de processos (decisão de 2026-10-18). Com documento, usa a pessoa
/// que já o tem; sem documento, a que tem exatamente o mesmo nome e nenhum documento; senão, cadastra (CPF: pessoa
/// física; CNPJ ou sem documento: jurídica). Garante o papel (cliente ou parte contrária). Grava na hora.
/// Implementado pelo Crm.
/// </summary>
public interface IPeopleRegistry
{
    /// <summary>Os ids das pessoas, na mesma ordem das sementes.</summary>
    Task<IReadOnlyList<Guid>> EnsureAsync(IReadOnlyList<PersonSeed> seeds, CancellationToken cancellationToken);
}

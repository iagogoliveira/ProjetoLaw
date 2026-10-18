using LexFlow.Crm.Domain.Conflicts;
using LexFlow.Crm.Domain.Opportunities;
using LexFlow.Crm.Domain.People;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.Abstractions;

/// <summary>Acesso aos dados do módulo pelos handlers (implementado pelo <c>CrmDbContext</c>).</summary>
public interface ICrmDbContext
{
    DbSet<Person> People { get; }

    DbSet<EconomicGroup> EconomicGroups { get; }

    DbSet<ConflictCheck> ConflictChecks { get; }

    DbSet<Opportunity> Opportunities { get; }

    DbSet<Proposal> Proposals { get; }

    DbSet<ProposalTemplate> ProposalTemplates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Funções do PostgreSQL usadas nas consultas LINQ (mapeadas no modelo do EF). <c>word_similarity</c> (pg_trgm)
/// mede o quanto o termo aparece dentro do texto: "joao da silva" em "joao da silva ltda" vale 1.
/// </summary>
public static class CrmDbFunctions
{
    public static double WordSimilarity(string term, string text) =>
        throw new InvalidOperationException("Só pode ser usada em consultas LINQ traduzidas para SQL.");

    public static double Similarity(string term, string text) =>
        throw new InvalidOperationException("Só pode ser usada em consultas LINQ traduzidas para SQL.");
}

/// <summary>Dados de uma empresa na Receita Federal, para preencher o cadastro.</summary>
public sealed record CompanyRegistration(
    string Cnpj,
    string LegalName,
    string? TradeName,
    string? Email,
    string? Phone,
    string? Status,
    AddressInput? Address);

/// <summary>Resultado da consulta: a empresa, nenhuma (CNPJ inexistente) ou serviço indisponível.</summary>
public sealed record CnpjLookupResult(CompanyRegistration? Company, bool Unavailable)
{
    public static CnpjLookupResult Found(CompanyRegistration company) => new(company, false);

    public static CnpjLookupResult NotFound { get; } = new(null, false);

    public static CnpjLookupResult ServiceUnavailable { get; } = new(null, true);
}

/// <summary>
/// Consulta de CNPJ na Receita Federal (Etapa 4, tarefa 4; decisão de 2026-10-17: BrasilAPI), para preencher e
/// atualizar o cadastro. Implementações na Infrastructure; serviço fora do ar não impede o cadastro manual.
/// </summary>
public interface ICnpjLookup
{
    Task<CnpjLookupResult> LookupAsync(string cnpj, CancellationToken cancellationToken);
}

/// <summary>Parte de um processo, como o módulo de processos a vê (lado em relação ao cliente do escritório).</summary>
public sealed record CaseParty(Guid CaseId, string CaseNumber, string PartyName, string? PartyDocument, CasePartySide Side, string ClientName);

public enum CasePartySide
{
    Client,
    Opponent,
}

/// <summary>
/// Partes dos processos do escritório, para o conflito de interesses (Etapa 4, tarefa 3). Os processos chegam na
/// Etapa 5; até lá a implementação registrada não acha nada (decisão de 2026-10-17: stub agora, prova com processos
/// reais na Etapa 5).
/// </summary>
public interface ILitigationLookup
{
    /// <summary>Partes com o nome normalizado (<c>SearchText</c>) ou o documento (só dígitos) informados.</summary>
    Task<IReadOnlyList<CaseParty>> FindPartiesAsync(string searchName, string? document, CancellationToken cancellationToken);
}

/// <summary>Um item da proposta como entra no documento.</summary>
public sealed record ProposalDocumentItem(string Description, FeeKind Kind, decimal Quantity, decimal? UnitAmount, decimal? Percentage, decimal? Amount);

/// <summary>Os valores dos marcadores do modelo (<see cref="IProposalDocuments.Markers"/>).</summary>
public sealed record ProposalDocumentData(
    string ClientName,
    string? ClientDocument,
    string? ClientEmail,
    string OpportunityTitle,
    string Title,
    string? Scope,
    string? Conditions,
    DateOnly Date,
    DateOnly? ValidUntil,
    decimal Total,
    IReadOnlyList<ProposalDocumentItem> Items);

/// <summary>
/// Modelos DOCX de proposta com mesclagem simples (Etapa 4; o motor completo, com laços e condicionais, é da Etapa 9):
/// marcadores <c>{{grupo.campo}}</c> no texto e uma linha de tabela com marcadores <c>{{item.*}}</c>, repetida por
/// item. Implementado na Infrastructure sobre o Open XML SDK.
/// </summary>
public interface IProposalDocuments
{
    /// <summary>Marcadores aceitos, para a tela e para a validação do upload.</summary>
    IReadOnlyList<string> Markers { get; }

    /// <summary>O modelo padrão do sistema (também serve de ponto de partida para o escritório).</summary>
    byte[] DefaultTemplate();

    /// <summary>Confere se o arquivo é um DOCX e se só usa marcadores conhecidos.</summary>
    Result Validate(Stream docx);

    /// <summary>Gera a proposta a partir do modelo (o padrão, quando <paramref name="model"/> é nulo).</summary>
    byte[] Render(Stream? model, ProposalDocumentData data);
}

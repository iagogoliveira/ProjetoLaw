using LexFlow.Crm.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.Abstractions;

/// <summary>Acesso aos dados do módulo pelos handlers (implementado pelo <c>CrmDbContext</c>).</summary>
public interface ICrmDbContext
{
    DbSet<Person> People { get; }

    DbSet<EconomicGroup> EconomicGroups { get; }

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

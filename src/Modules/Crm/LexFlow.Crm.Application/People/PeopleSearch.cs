using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.People;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.People;

/// <summary>Posição na lista em ordem alfabética (paginação por cursor, seção 4.4).</summary>
public sealed record PeoplePosition(string SearchName, Guid Id);

public sealed record PeoplePage(IReadOnlyList<PersonSummary> Items, PeoplePosition? Next);

/// <summary>
/// Lista e busca de pessoas (Etapa 4, tarefa 2). Sem termo: ordem alfabética com cursor. Com termo: por nome (sem
/// acentos, aproximado com pg_trgm) ou por documento, ordenado pela semelhança e limitado a <c>Limit</c> itens, sem
/// cursor ("Joao da Silva" encontra "João da Silva Ltda.").
/// </summary>
public sealed record SearchPeopleQuery(string? Term, PersonRoleKind? Role, PeoplePosition? After, int Limit) : IQuery<PeoplePage>;

internal sealed class SearchPeopleValidator : AbstractValidator<SearchPeopleQuery>
{
    public SearchPeopleValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}

internal sealed class SearchPeopleHandler(ICrmDbContext db) : IQueryHandler<SearchPeopleQuery, PeoplePage>
{
    /// <summary>A partir daqui o nome conta como encontrado (escala do <c>word_similarity</c>, 0 a 1).</summary>
    public const double MinimumWordSimilarity = 0.5;

    /// <summary>Só os dígitos do texto (CPF ou CNPJ digitado com ou sem pontuação).</summary>
    public static string DigitsOf(string? text) => new([.. (text ?? string.Empty).Where(char.IsAsciiDigit)]);

    public async Task<Result<PeoplePage>> Handle(SearchPeopleQuery query, CancellationToken cancellationToken)
    {
        var people = db.People.AsNoTracking();
        if (query.Role is { } role)
        {
            people = people.Where(p => p.Roles.Any(r => r.Role == role));
        }

        var term = SearchText.Normalize(query.Term);
        if (term.Length == 0)
        {
            if (query.After is { } after)
            {
                people = people.Where(p => p.SearchName.CompareTo(after.SearchName) > 0
                    || (p.SearchName == after.SearchName && p.Id.CompareTo(after.Id) > 0));
            }

            var page = await people.OrderBy(p => p.SearchName).ThenBy(p => p.Id).Take(query.Limit + 1)
                .Select(p => new { p.Id, p.Kind, p.Name, p.AlternateName, p.Document, p.SearchName, Roles = p.Roles.Select(r => r.Role).ToList() })
                .ToListAsync(cancellationToken);
            var items = page.Take(query.Limit).Select(p => new PersonSummary(p.Id, p.Kind, p.Name, p.AlternateName, p.Document, [.. p.Roles.Order()], null)).ToList();
            var last = page.Count > query.Limit ? page[query.Limit - 1] : null;
            return new PeoplePage(items, last is null ? null : new PeoplePosition(last.SearchName, last.Id));
        }

        var digits = DigitsOf(query.Term);
        var byDocument = digits.Length >= 3 ? digits : null;
        var found = await people
            .Select(p => new
            {
                p.Id,
                p.Kind,
                p.Name,
                p.AlternateName,
                p.Document,
                Roles = p.Roles.Select(r => r.Role).ToList(),
                Score = CrmDbFunctions.WordSimilarity(term, p.SearchName),
                Contains = p.SearchName.Contains(term),
                DocumentMatch = byDocument != null && p.Document != null && p.Document.StartsWith(byDocument),
            })
            .Where(p => p.Contains || p.DocumentMatch || p.Score >= MinimumWordSimilarity)
            .OrderByDescending(p => p.DocumentMatch).ThenByDescending(p => p.Score).ThenBy(p => p.Name)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);

        return new PeoplePage(
            [.. found.Select(p => new PersonSummary(p.Id, p.Kind, p.Name, p.AlternateName, p.Document, [.. p.Roles.Order()], Math.Round(p.Score, 3)))],
            null);
    }
}

/// <summary>Possível duplicidade (RF-06): mesmo documento ou nome parecido, para avisar antes de cadastrar.</summary>
public sealed record SimilarPerson(Guid Id, PersonKind Kind, string Name, string? Document, bool SameDocument, double Score);

public sealed record FindSimilarPeopleQuery(string? Name, string? Document, Guid? ExcludeId) : IQuery<IReadOnlyList<SimilarPerson>>;

internal sealed class FindSimilarPeopleHandler(ICrmDbContext db) : IQueryHandler<FindSimilarPeopleQuery, IReadOnlyList<SimilarPerson>>
{
    /// <summary>Semelhança do nome inteiro (<c>similarity</c>) a partir da qual o cadastro é apontado como possível duplicata.</summary>
    public const double MinimumSimilarity = 0.6;

    public async Task<Result<IReadOnlyList<SimilarPerson>>> Handle(FindSimilarPeopleQuery query, CancellationToken cancellationToken)
    {
        var name = SearchText.Normalize(query.Name);
        var document = SearchPeopleHandler.DigitsOf(query.Document);
        if (name.Length == 0 && document.Length == 0)
        {
            return Array.Empty<SimilarPerson>();
        }

        var matches = await db.People.AsNoTracking()
            .Where(p => p.Id != query.ExcludeId)
            .Select(p => new
            {
                p.Id,
                p.Kind,
                p.Name,
                p.Document,
                SameDocument = document.Length > 0 && p.Document == document,
                Score = name.Length > 0 ? CrmDbFunctions.Similarity(name, p.SearchName) : 0,
            })
            .Where(p => p.SameDocument || p.Score >= MinimumSimilarity)
            .OrderByDescending(p => p.SameDocument).ThenByDescending(p => p.Score)
            .Take(10)
            .ToListAsync(cancellationToken);

        return matches.Select(m => new SimilarPerson(m.Id, m.Kind, m.Name, m.Document, m.SameDocument, Math.Round(m.Score, 3))).ToList();
    }
}

/// <summary>Consulta o CNPJ na Receita Federal (BrasilAPI) para preencher o cadastro de pessoa jurídica.</summary>
public sealed record LookupCnpjQuery(string Cnpj) : IQuery<CompanyRegistration>;

internal sealed class LookupCnpjValidator : AbstractValidator<LookupCnpjQuery>
{
    public LookupCnpjValidator() => RuleFor(q => q.Cnpj).Must(Cnpj.IsValid).WithErrorCode(CrmErrors.InvalidCnpj.Code);
}

internal sealed class LookupCnpjHandler(ICnpjLookup lookup) : IQueryHandler<LookupCnpjQuery, CompanyRegistration>
{
    public async Task<Result<CompanyRegistration>> Handle(LookupCnpjQuery query, CancellationToken cancellationToken)
    {
        var result = await lookup.LookupAsync(Cnpj.Create(query.Cnpj).Value.Value, cancellationToken);
        return result switch
        {
            { Company: { } company } => company,
            { Unavailable: true } => CrmErrors.CnpjLookupUnavailable,
            _ => CrmErrors.CnpjNotFound,
        };
    }
}

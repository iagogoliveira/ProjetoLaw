using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Contracts;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Platform.Contracts.Courts;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Cases;

public sealed record GetCaseQuery(Guid Id) : IQuery<CaseResponse>;

internal sealed class GetCaseHandler(ILitigationDbContext db, CaseResponseBuilder responses) : IQueryHandler<GetCaseQuery, CaseResponse>
{
    public async Task<Result<CaseResponse>> Handle(GetCaseQuery query, CancellationToken cancellationToken)
    {
        var legalCase = await db.Cases.AsNoTracking().Include(c => c.Parties).Include(c => c.Assignments)
            .SingleOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
        return legalCase is null ? LitigationErrors.CaseNotFound : await responses.BuildAsync(legalCase, cancellationToken);
    }
}

/// <summary>Linha da lista: pasta, CNJ, tipo, situação, área e os clientes (nomes do Crm).</summary>
public sealed record CaseSummary(
    Guid Id,
    CaseKind Kind,
    string InternalFolder,
    string? CnjNumber,
    CaseStatus Status,
    Guid? PracticeAreaId,
    Guid? PhaseId,
    IReadOnlyList<string> Clients,
    DateTimeOffset CreatedAt);

/// <summary>Posição na lista (mais recentes primeiro).</summary>
public sealed record CasesPosition(DateTimeOffset CreatedAt, Guid Id);

public sealed record CasesPage(IReadOnlyList<CaseSummary> Items, CasesPosition? Next);

/// <summary>
/// Lista de processos e casos, dos mais recentes aos mais antigos, com busca pela pasta interna ou pelo número CNJ
/// (com ou sem pontuação) e filtros por situação e área. Os filtros completos e o teste de carga chegam no PR 5B.
/// </summary>
public sealed record ListCasesQuery(string? Term, CaseStatus? Status, Guid? PracticeAreaId, CasesPosition? After, int Limit) : IQuery<CasesPage>;

internal sealed class ListCasesValidator : AbstractValidator<ListCasesQuery>
{
    public ListCasesValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}

internal sealed class ListCasesHandler(ILitigationDbContext db, IPeopleDirectory people) : IQueryHandler<ListCasesQuery, CasesPage>
{
    public async Task<Result<CasesPage>> Handle(ListCasesQuery query, CancellationToken cancellationToken)
    {
        var cases = db.Cases.AsNoTracking();
        if (query.Status is { } status)
        {
            cases = cases.Where(c => c.Status == status);
        }

        if (query.PracticeAreaId is { } area)
        {
            cases = cases.Where(c => c.PracticeAreaId == area);
        }

        if (!string.IsNullOrWhiteSpace(query.Term))
        {
            var term = query.Term.Trim().ToUpperInvariant();
            var digits = new string([.. term.Where(char.IsAsciiDigit)]);
            cases = cases.Where(c => c.InternalFolder.StartsWith(term) || (digits.Length > 0 && c.CnjNumber != null && c.CnjNumber.StartsWith(digits)));
        }

        if (query.After is { } after)
        {
            cases = cases.Where(c => c.CreatedAt < after.CreatedAt || (c.CreatedAt == after.CreatedAt && c.Id.CompareTo(after.Id) < 0));
        }

        var page = await cases.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Take(query.Limit + 1)
            .Select(c => new
            {
                c.Id,
                c.Kind,
                c.InternalFolder,
                c.CnjNumber,
                c.Status,
                c.PracticeAreaId,
                c.PhaseId,
                c.CreatedAt,
                Clients = c.Parties.Where(p => p.IsClient).Select(p => p.PersonId).ToList(),
            })
            .ToListAsync(cancellationToken);
        var names = (await people.GetAsync([.. page.SelectMany(p => p.Clients).Distinct()], cancellationToken)).ToDictionary(p => p.Id, p => p.Name);
        var items = page.Take(query.Limit)
            .Select(c => new CaseSummary(
                c.Id, c.Kind, c.InternalFolder, CnjFormat.Of(c.CnjNumber), c.Status, c.PracticeAreaId, c.PhaseId,
                [.. c.Clients.Select(id => names.GetValueOrDefault(id, "—"))], c.CreatedAt))
            .ToList();
        var last = page.Count > query.Limit ? page[query.Limit - 1] : null;
        return new CasesPage(items, last is null ? null : new CasesPosition(last.CreatedAt, last.Id));
    }
}

/// <summary>O que o número CNJ diz: a pontuação, o ano, o segmento (J), o tribunal (TR) e a origem (Etapa 5, tarefa 3).</summary>
public sealed record CnjInfoResponse(string Formatted, int Year, int Segment, string CourtCode, string Origin, CourtResponse? Court);

public sealed record LookupCnjQuery(string Number) : IQuery<CnjInfoResponse>;

internal sealed class LookupCnjHandler(ICourtDirectory courts) : IQueryHandler<LookupCnjQuery, CnjInfoResponse>
{
    public async Task<Result<CnjInfoResponse>> Handle(LookupCnjQuery query, CancellationToken cancellationToken)
    {
        var number = SharedKernel.ValueObjects.CnjNumber.Create(query.Number);
        if (number.IsFailure)
        {
            return LitigationErrors.CnjInvalid;
        }

        var cnj = number.Value;
        var court = await courts.FindAsync(cnj.Segment, cnj.Court, cancellationToken);
        return new CnjInfoResponse(cnj.Formatted, cnj.Year, cnj.Segment, cnj.Court, cnj.Origin, court is null ? null : new CourtResponse(court.Id, court.Acronym, court.Name, court.State));
    }
}

using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Contracts;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Application.Access;
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

/// <summary>Filtros da lista; todos opcionais e combinados com "e".</summary>
public sealed record CaseFilters(
    string? Term = null,
    CaseStatus? Status = null,
    CaseKind? Kind = null,
    Guid? PracticeAreaId = null,
    Guid? BranchId = null,
    Guid? PhaseId = null,
    Guid? ResponsibleUserId = null,
    Guid? TeamId = null,
    Guid? PersonId = null);

/// <summary>
/// Lista de processos e casos ao alcance do usuário (escopo de dados e casos sigilosos), dos mais recentes aos mais
/// antigos, com busca pela pasta interna ou pelo número CNJ (com ou sem pontuação) e filtros por situação, tipo,
/// área, filial, fase, responsável, equipe e pessoa (parte).
/// </summary>
public sealed record ListCasesQuery(CaseFilters Filters, CasesPosition? After, int Limit) : IQuery<CasesPage>;

internal sealed class ListCasesValidator : AbstractValidator<ListCasesQuery>
{
    public ListCasesValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 200).WithErrorCode("pagination.limit_out_of_range");
}

internal sealed class ListCasesHandler(ILitigationDbContext db, IPeopleDirectory people, CaseAccess access) : IQueryHandler<ListCasesQuery, CasesPage>
{
    public async Task<Result<CasesPage>> Handle(ListCasesQuery query, CancellationToken cancellationToken)
    {
        var context = await access.ContextAsync(CasePermissions.Read, cancellationToken);
        var cases = Filter(CaseAccess.Visible(db.Cases.AsNoTracking(), context), query.Filters);

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

    private static IQueryable<LegalCase> Filter(IQueryable<LegalCase> cases, CaseFilters f)
    {
        cases = f.Status is { } status ? cases.Where(c => c.Status == status) : cases;
        cases = f.Kind is { } kind ? cases.Where(c => c.Kind == kind) : cases;
        cases = f.PracticeAreaId is { } area ? cases.Where(c => c.PracticeAreaId == area) : cases;
        cases = f.BranchId is { } branch ? cases.Where(c => c.BranchId == branch) : cases;
        cases = f.PhaseId is { } phase ? cases.Where(c => c.PhaseId == phase) : cases;
        cases = f.ResponsibleUserId is { } responsible ? cases.Where(c => c.Assignments.Any(a => a.UserId == responsible)) : cases;
        cases = f.TeamId is { } team ? cases.Where(c => c.Assignments.Any(a => a.TeamId == team)) : cases;
        cases = f.PersonId is { } person ? cases.Where(c => c.Parties.Any(p => p.PersonId == person)) : cases;
        if (!string.IsNullOrWhiteSpace(f.Term))
        {
            var term = f.Term.Trim().ToUpperInvariant();
            var digits = new string([.. term.Where(char.IsAsciiDigit)]);
            cases = cases.Where(c => c.InternalFolder.StartsWith(term) || (digits.Length > 0 && c.CnjNumber != null && c.CnjNumber.StartsWith(digits)));
        }

        return cases;
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

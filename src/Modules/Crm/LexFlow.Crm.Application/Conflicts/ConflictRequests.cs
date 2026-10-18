using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Application.People;
using LexFlow.Crm.Contracts;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Conflicts;
using LexFlow.Crm.Domain.People;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.Conflicts;

public sealed record ConflictHitResponse(ConflictRisk Risk, ConflictReason Reason, Guid? PersonId, string MatchedName, string? CaseNumber, double? Score);

public sealed record ConflictCheckResponse(
    Guid Id,
    string QueryName,
    string? QueryDocument,
    ConflictSide Side,
    ConflictRisk Risk,
    bool Proceeded,
    DateTimeOffset? ProceededAt,
    string? Justification,
    Guid CheckedBy,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ConflictHitResponse> Hits);

internal static class ConflictMapping
{
    public static ConflictCheckResponse ToResponse(this ConflictCheck c) => new(
        c.Id,
        c.QueryName,
        c.QueryDocument,
        c.Side,
        c.Risk,
        c.Proceeded,
        c.ProceededAt,
        c.Justification,
        c.CreatedBy,
        c.CreatedAt,
        [.. c.Hits.OrderByDescending(h => h.Risk).ThenByDescending(h => h.Score ?? 1).Select(h => new ConflictHitResponse(h.Risk, h.Reason, h.PersonId, h.MatchedName, h.CaseNumber, h.Score))]);
}

/// <summary>
/// Detecção de conflito (RF-07). O lado oposto ao consultado é o que gera conflito: futuro cliente que já é parte
/// contrária, ou futura parte contrária que já é cliente.
/// <list type="bullet">
/// <item>Direto: mesmo documento ou mesmo nome de uma pessoa do lado oposto, ou parte do lado oposto num processo de
/// outro cliente.</item>
/// <item>Indireto (decisão de 2026-10-17): nome parecido com o de uma pessoa do lado oposto, mesmo grupo econômico de
/// uma pessoa do lado oposto, ou e-mail de contato compartilhado com ela.</item>
/// </list>
/// </summary>
internal sealed class ConflictDetector(ICrmDbContext db, ILitigationLookup litigation)
{
    /// <summary>Semelhança do nome inteiro (<c>similarity</c>) a partir da qual o nome parecido vira conflito indireto.</summary>
    public const double MinimumSimilarity = 0.6;

    public async Task<IReadOnlyList<ConflictHitInput>> DetectAsync(string name, string? document, ConflictSide side, IReadOnlyCollection<string> contactEmails, CancellationToken cancellationToken)
    {
        var query = new Query(SearchText.Normalize(name), document, side == ConflictSide.Client ? PersonRoleKind.Opponent : PersonRoleKind.Client);
        var hits = new Dictionary<string, ConflictHitInput>(StringComparer.Ordinal);

        void Add(string key, ConflictHitInput hit)
        {
            if (!hits.TryGetValue(key, out var current) || current.Risk < hit.Risk)
            {
                hits[key] = hit;
            }
        }

        foreach (var hit in await OppositeMatchesAsync(query, cancellationToken))
        {
            Add($"person:{hit.PersonId}", hit);
        }

        // A própria pessoa, se já cadastrada (com qualquer papel): o grupo econômico e os contatos dela.
        var identity = await db.People.AsNoTracking()
            .Where(p => (query.Document != null && p.Document == query.Document) || p.SearchName == query.Term)
            .Select(p => new { p.EconomicGroupId, p.Email, Emails = p.Contacts.Where(c => c.Email != null).Select(c => c.Email!).ToList() })
            .ToListAsync(cancellationToken);

        var groups = identity.Where(i => i.EconomicGroupId != null).Select(i => i.EconomicGroupId!.Value).Distinct().ToList();
        foreach (var hit in await SameGroupAsync(query, groups, cancellationToken))
        {
            Add($"person:{hit.PersonId}", hit);
        }

        var emails = contactEmails.Select(e => e.Trim().ToLowerInvariant())
            .Concat(identity.SelectMany(i => i.Emails).Concat(identity.Where(i => i.Email != null).Select(i => i.Email!)))
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var hit in await SharedContactAsync(query, emails, cancellationToken))
        {
            Add($"person:{hit.PersonId}", hit);
        }

        // Processos de outros clientes em que a pessoa já está do lado oposto.
        var caseSide = side == ConflictSide.Client ? CasePartySide.Opponent : CasePartySide.Client;
        foreach (var party in (await litigation.FindPartiesAsync(query.Term, document, cancellationToken)).Where(p => p.Side == caseSide))
        {
            Add($"case:{party.CaseId}", new ConflictHitInput(ConflictRisk.Direct, ConflictReason.CaseParty, null, $"{party.PartyName} (processo de {party.ClientName})", party.CaseNumber, null));
        }

        return [.. hits.Values];
    }

    /// <summary>Pessoas do lado oposto com o mesmo documento, o mesmo nome (diretos) ou nome parecido (indireto).</summary>
    private async Task<IEnumerable<ConflictHitInput>> OppositeMatchesAsync(Query query, CancellationToken cancellationToken)
    {
        var candidates = await db.People.AsNoTracking()
            .Where(p => p.Roles.Any(r => r.Role == query.Opposite))
            .Select(p => new
            {
                p.Id,
                p.Name,
                SameDocument = query.Document != null && p.Document == query.Document,
                SameName = p.SearchName == query.Term,
                Score = CrmDbFunctions.Similarity(query.Term, p.SearchName),
            })
            .Where(p => p.SameDocument || p.SameName || p.Score >= MinimumSimilarity)
            .ToListAsync(cancellationToken);

        return candidates.Select(c => c.SameDocument
            ? new ConflictHitInput(ConflictRisk.Direct, ConflictReason.SameDocument, c.Id, c.Name, null, null)
            : c.SameName
                ? new ConflictHitInput(ConflictRisk.Direct, ConflictReason.SameName, c.Id, c.Name, null, null)
                : new ConflictHitInput(ConflictRisk.Indirect, ConflictReason.SimilarName, c.Id, c.Name, null, Math.Round(c.Score, 3)));
    }

    /// <summary>Pessoas do lado oposto nos grupos econômicos da pessoa consultada.</summary>
    private async Task<IEnumerable<ConflictHitInput>> SameGroupAsync(Query query, List<Guid> groups, CancellationToken cancellationToken)
    {
        if (groups.Count == 0)
        {
            return [];
        }

        var related = await db.People.AsNoTracking()
            .Where(p => p.EconomicGroupId != null && groups.Contains(p.EconomicGroupId.Value) && p.Roles.Any(r => r.Role == query.Opposite))
            .Where(p => !(query.Document != null && p.Document == query.Document) && p.SearchName != query.Term)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);
        return related.Select(r => new ConflictHitInput(ConflictRisk.Indirect, ConflictReason.SameEconomicGroup, r.Id, r.Name, null, null));
    }

    /// <summary>Pessoas do lado oposto com e-mail (próprio ou de contato) igual a um da pessoa consultada.</summary>
    private async Task<IEnumerable<ConflictHitInput>> SharedContactAsync(Query query, List<string> emails, CancellationToken cancellationToken)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        var shared = await db.People.AsNoTracking()
            .Where(p => p.Roles.Any(r => r.Role == query.Opposite))
            .Where(p => (p.Email != null && emails.Contains(p.Email)) || p.Contacts.Any(c => c.Email != null && emails.Contains(c.Email)))
            .Where(p => !(query.Document != null && p.Document == query.Document) && p.SearchName != query.Term)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);
        return shared.Select(s => new ConflictHitInput(ConflictRisk.Indirect, ConflictReason.SharedContact, s.Id, s.Name, null, null));
    }

    /// <summary>Termo normalizado, documento (só dígitos) e o papel que gera conflito.</summary>
    private sealed record Query(string Term, string? Document, PersonRoleKind Opposite);
}

/// <summary>
/// Verificação de conflito antes de abrir cliente ou caso (RF-07): grava o que foi consultado, o lado, o risco e os
/// achados, para o relatório de auditoria e compliance da OAB.
/// </summary>
public sealed record CheckConflictCommand(string Name, string? Document, ConflictSide Side, IReadOnlyList<string>? ContactEmails) : ICommand<ConflictCheckResponse>;

internal sealed class CheckConflictValidator : AbstractValidator<CheckConflictCommand>
{
    public CheckConflictValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithErrorCode("crm.name_required").MaximumLength(Person.MaxNameLength).WithErrorCode("crm.name_too_long");
        RuleFor(c => c.Document).Must(d => string.IsNullOrWhiteSpace(d) || Cpf.IsValid(d) || Cnpj.IsValid(d)).WithErrorCode("crm.document_invalid");
        RuleFor(c => c.Side).IsInEnum().WithErrorCode("crm.conflict_side_invalid");
        RuleForEach(c => c.ContactEmails).Must(Email.IsValid).WithErrorCode("crm.email_invalid");
    }
}

internal sealed class CheckConflictHandler(ICrmDbContext db, ConflictDetector detector, IIdGenerator ids) : ICommandHandler<CheckConflictCommand, ConflictCheckResponse>
{
    public async Task<Result<ConflictCheckResponse>> Handle(CheckConflictCommand command, CancellationToken cancellationToken)
    {
        var document = string.IsNullOrWhiteSpace(command.Document) ? null : SearchPeopleHandler.DigitsOf(command.Document);
        var hits = await detector.DetectAsync(command.Name, document, command.Side, command.ContactEmails ?? [], cancellationToken);
        var check = ConflictCheck.Record(ids.NewId(), command.Name, document, command.Side, hits);
        db.ConflictChecks.Add(check);
        await db.SaveChangesAsync(cancellationToken);
        return check.ToResponse();
    }
}

/// <summary>Decide seguir com o novo cliente ou caso; com conflito apontado, a justificativa é obrigatória (RF-07).</summary>
public sealed record ProceedConflictCommand(Guid Id, string? Justification) : ICommand<ConflictCheckResponse>;

internal sealed class ProceedConflictValidator : AbstractValidator<ProceedConflictCommand>
{
    public ProceedConflictValidator() =>
        RuleFor(c => c.Justification).MaximumLength(ConflictCheck.MaxJustificationLength).WithErrorCode("crm.conflict_justification_too_long");
}

internal sealed class ProceedConflictHandler(ICrmDbContext db, IClock clock) : ICommandHandler<ProceedConflictCommand, ConflictCheckResponse>
{
    public async Task<Result<ConflictCheckResponse>> Handle(ProceedConflictCommand command, CancellationToken cancellationToken)
    {
        var check = await db.ConflictChecks.Include(c => c.Hits).SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
        if (check is null)
        {
            return CrmErrors.ConflictCheckNotFound;
        }

        var proceeded = check.Proceed(command.Justification, clock.UtcNow);
        if (proceeded.IsFailure)
        {
            return proceeded.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return check.ToResponse();
    }
}

public sealed record GetConflictCheckQuery(Guid Id) : IQuery<ConflictCheckResponse>;

internal sealed class GetConflictCheckHandler(ICrmDbContext db) : IQueryHandler<GetConflictCheckQuery, ConflictCheckResponse>
{
    public async Task<Result<ConflictCheckResponse>> Handle(GetConflictCheckQuery query, CancellationToken cancellationToken)
    {
        var check = await db.ConflictChecks.AsNoTracking().Include(c => c.Hits).SingleOrDefaultAsync(c => c.Id == query.Id, cancellationToken);
        return check is null ? CrmErrors.ConflictCheckNotFound : check.ToResponse();
    }
}

/// <summary>Posição no relatório (mais recentes primeiro).</summary>
public sealed record ConflictChecksPosition(DateTimeOffset CreatedAt, Guid Id);

public sealed record ConflictChecksPage(IReadOnlyList<ConflictCheckResponse> Items, ConflictChecksPosition? Next);

/// <summary>Relatório das verificações realizadas (RF-07), das mais recentes às mais antigas, com filtro por risco.</summary>
public sealed record ListConflictChecksQuery(ConflictRisk? Risk, ConflictChecksPosition? After, int Limit) : IQuery<ConflictChecksPage>;

internal sealed class ListConflictChecksHandler(ICrmDbContext db) : IQueryHandler<ListConflictChecksQuery, ConflictChecksPage>
{
    public async Task<Result<ConflictChecksPage>> Handle(ListConflictChecksQuery query, CancellationToken cancellationToken)
    {
        var checks = db.ConflictChecks.AsNoTracking().Include(c => c.Hits).AsQueryable();
        if (query.Risk is { } risk)
        {
            checks = checks.Where(c => c.Risk == risk);
        }

        if (query.After is { } after)
        {
            checks = checks.Where(c => c.CreatedAt < after.CreatedAt || (c.CreatedAt == after.CreatedAt && c.Id.CompareTo(after.Id) < 0));
        }

        var page = await checks.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Take(query.Limit + 1).ToListAsync(cancellationToken);
        var items = page.Take(query.Limit).Select(c => c.ToResponse()).ToList();
        var last = page.Count > query.Limit ? page[query.Limit - 1] : null;
        return new ConflictChecksPage(items, last is null ? null : new ConflictChecksPosition(last.CreatedAt, last.Id));
    }
}

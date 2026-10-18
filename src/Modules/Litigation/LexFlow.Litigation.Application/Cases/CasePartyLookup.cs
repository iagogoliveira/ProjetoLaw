using LexFlow.Crm.Contracts;
using LexFlow.Litigation.Application.Abstractions;
using LexFlow.Litigation.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Litigation.Application.Cases;

/// <summary>
/// Partes dos processos para o conflito de interesses do Crm (substitui o stub da Etapa 4): as pessoas com o nome ou o
/// documento consultados, em quais processos estão e de que lado (cliente do escritório ou parte contrária), com o
/// nome do cliente de cada processo.
/// </summary>
internal sealed class CasePartyLookup(ILitigationDbContext db, IPeopleDirectory people) : ILitigationLookup
{
    public async Task<IReadOnlyList<CasePartyMatch>> FindPartiesAsync(string searchName, string? document, CancellationToken cancellationToken)
    {
        var personIds = await people.FindAsync(searchName, document, cancellationToken);
        if (personIds.Count == 0)
        {
            return [];
        }

        var cases = await db.Cases.AsNoTracking().Include(c => c.Parties)
            .Where(c => c.Parties.Any(p => personIds.Contains(p.PersonId)))
            .ToListAsync(cancellationToken);
        var cards = (await people.GetAsync([.. cases.SelectMany(c => c.Parties.Select(p => p.PersonId)).Distinct()], cancellationToken)).ToDictionary(p => p.Id);
        string NameOf(Guid id) => cards.TryGetValue(id, out var card) ? card.Name : "—";

        return [.. cases.SelectMany(c => c.Parties
            .Where(p => personIds.Contains(p.PersonId))
            .Select(p => new CasePartyMatch(
                c.Id,
                CnjFormat.Of(c.CnjNumber) ?? c.InternalFolder,
                NameOf(p.PersonId),
                cards.TryGetValue(p.PersonId, out var card) ? card.Document : null,
                p.IsClient ? CasePartySide.Client : CasePartySide.Opponent,
                string.Join(", ", c.Parties.Where(x => x.IsClient).Select(x => NameOf(x.PersonId))))))];
    }
}

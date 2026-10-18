using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Contracts;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.People;

/// <summary>O cadastro de pessoas para os outros módulos, na organização ativa (filtro de tenant e RLS).</summary>
internal sealed class PeopleDirectory(ICrmDbContext db) : IPeopleDirectory
{
    public async Task<IReadOnlyList<PersonCard>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var people = await db.People.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Kind, p.Name, p.Document, p.AnonymizedAt })
            .ToListAsync(cancellationToken);
        return [.. people.Select(p => new PersonCard(p.Id, p.Kind.ToString(), p.Name, p.Document, p.AnonymizedAt != null))];
    }

    public async Task<IReadOnlyList<Guid>> FindAsync(string searchName, string? document, CancellationToken cancellationToken) =>
        await db.People.AsNoTracking()
            .Where(p => p.SearchName == searchName || (document != null && p.Document == document))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
}

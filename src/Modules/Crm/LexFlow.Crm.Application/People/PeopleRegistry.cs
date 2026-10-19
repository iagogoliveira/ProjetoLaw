using LexFlow.Application.Abstractions;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Contracts;
using LexFlow.Crm.Domain.People;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.People;

/// <summary>
/// Pessoas da importação de processos: reaproveita pelo documento ou, sem documento, pelo nome exato (sem acentos e
/// sem diferença de maiúsculas); senão, cadastra. Repetições dentro do mesmo lote viram a mesma pessoa.
/// </summary>
internal sealed class PeopleRegistry(ICrmDbContext db, IIdGenerator ids) : IPeopleRegistry
{
    public async Task<IReadOnlyList<Guid>> EnsureAsync(IReadOnlyList<PersonSeed> seeds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        var keyed = seeds.Select(s => (Seed: s, Document: NormalizeDocument(s.Document), Name: SearchText.Normalize(s.Name))).ToList();
        var documents = keyed.Where(k => k.Document is not null).Select(k => k.Document!).Distinct(StringComparer.Ordinal).ToList();
        var names = keyed.Where(k => k.Document is null).Select(k => k.Name).Distinct(StringComparer.Ordinal).ToList();

        var existing = await db.People.Include(p => p.Roles)
            .Where(p => (p.Document != null && documents.Contains(p.Document)) || (p.Document == null && names.Contains(p.SearchName)))
            .ToListAsync(cancellationToken);
        var byKey = new Dictionary<string, Person>(StringComparer.Ordinal);
        foreach (var person in existing)
        {
            byKey.TryAdd(person.Document is { } document ? "d:" + document : "n:" + person.SearchName, person);
        }

        var result = new List<Guid>(seeds.Count);
        foreach (var (seed, document, name) in keyed)
        {
            var key = document is null ? "n:" + name : "d:" + document;
            if (!byKey.TryGetValue(key, out var person))
            {
                person = Create(seed, document);
                db.People.Add(person);
                byKey[key] = person;
            }

            person.AddRole(seed.Role == PersonSeedRole.Client ? PersonRoleKind.Client : PersonRoleKind.Opponent);
            result.Add(person.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>CPF ou CNPJ só com os dígitos (conferidos); documento inválido conta como ausente.</summary>
    private static string? NormalizeDocument(string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return null;
        }

        var cpf = Cpf.Create(document);
        if (cpf.IsSuccess)
        {
            return cpf.Value.Value;
        }

        var cnpj = Cnpj.Create(document);
        return cnpj.IsSuccess ? cnpj.Value.Value : null;
    }

    private Person Create(PersonSeed seed, string? document)
    {
        var kind = document is { Length: 11 } ? PersonKind.Individual : PersonKind.Company;
        var role = seed.Role == PersonSeedRole.Client ? PersonRoleKind.Client : PersonRoleKind.Opponent;
        var created = Person.Create(ids.NewId(), kind, new PersonDetails(seed.Name.Trim(), null, document, null, null, null, null, null, [role], [], []));

        // O documento já foi conferido na validação da importação; um inválido aqui é erro de programação.
        return created.IsSuccess ? created.Value : throw new InvalidOperationException($"Pessoa inválida na importação: {created.Error!.Code}");
    }
}

using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Contracts;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.People;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.People;

/// <summary>
/// Fusão de cadastros duplicados (Etapa 4, tarefa 5): <see cref="KeepId"/> absorve <see cref="DuplicateId"/>, que
/// fica excluído logicamente e apontando para o mantido. Os achados de conflito passam para o mantido, e os demais
/// módulos recebem <see cref="PeopleMergedIntegrationEvent"/>. A trilha de auditoria registra as duas alterações.
/// </summary>
public sealed record MergePeopleCommand(Guid KeepId, Guid DuplicateId) : ICommand<PersonResponse>;

internal sealed class MergePeopleHandler(ICrmDbContext db, IIntegrationEventPublisher events) : ICommandHandler<MergePeopleCommand, PersonResponse>
{
    public async Task<Result<PersonResponse>> Handle(MergePeopleCommand command, CancellationToken cancellationToken)
    {
        if (command.KeepId == command.DuplicateId)
        {
            return CrmErrors.MergeSamePerson;
        }

        var people = await db.People.Include(p => p.Roles).Include(p => p.Addresses).Include(p => p.Contacts)
            .Where(p => p.Id == command.KeepId || p.Id == command.DuplicateId)
            .ToListAsync(cancellationToken);
        var keep = people.SingleOrDefault(p => p.Id == command.KeepId);
        var duplicate = people.SingleOrDefault(p => p.Id == command.DuplicateId);
        if (keep is null || duplicate is null)
        {
            return CrmErrors.PersonNotFound;
        }

        var absorbed = keep.Absorb(duplicate);
        if (absorbed.IsFailure)
        {
            return absorbed.Error;
        }

        var hits = await db.ConflictChecks.SelectMany(c => c.Hits).Where(h => h.PersonId == duplicate.Id).ToListAsync(cancellationToken);
        foreach (var hit in hits)
        {
            hit.PointTo(keep.Id, keep.Name);
        }

        db.People.Remove(duplicate);
        events.Publish(new PeopleMergedIntegrationEvent(keep.Id, duplicate.Id));
        await db.SaveChangesAsync(cancellationToken);

        // O índice único não aceita os dois com o mesmo documento: o mantido adota o documento depois de o duplicado
        // soltá-lo (as duas gravações ficam na mesma transação do caso de uso).
        if (absorbed.Value is { } document)
        {
            keep.AdoptDocument(document);
            await db.SaveChangesAsync(cancellationToken);
        }

        return keep.ToResponse();
    }
}

/// <summary>
/// Anonimização LGPD (Etapa 4, tarefa 7): substitui os dados pessoais mantendo o cadastro (e os vínculos). Exige o
/// motivo, que fica gravado no cadastro e na auditoria. Achados e consultas de conflito com o nome ou o documento da
/// pessoa também são anonimizados.
/// </summary>
public sealed record AnonymizePersonCommand(Guid Id, string Reason) : ICommand<PersonResponse>;

internal sealed class AnonymizePersonValidator : AbstractValidator<AnonymizePersonCommand>
{
    public AnonymizePersonValidator() =>
        RuleFor(c => c.Reason).NotEmpty().WithErrorCode("crm.anonymization_reason_required")
            .MaximumLength(500).WithErrorCode("crm.anonymization_reason_too_long");
}

internal sealed class AnonymizePersonHandler(ICrmDbContext db, IIntegrationEventPublisher events, IClock clock) : ICommandHandler<AnonymizePersonCommand, PersonResponse>
{
    public async Task<Result<PersonResponse>> Handle(AnonymizePersonCommand command, CancellationToken cancellationToken)
    {
        var person = await db.People.Include(p => p.Roles).Include(p => p.Addresses).Include(p => p.Contacts)
            .SingleOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (person is null)
        {
            return CrmErrors.PersonNotFound;
        }

        var (searchName, document) = (person.SearchName, person.Document);
        var anonymized = person.Anonymize(command.Reason, clock.UtcNow);
        if (anonymized.IsFailure)
        {
            return anonymized.Error;
        }

        var label = Person.AnonymizedName(person.Id);
        foreach (var hit in await db.ConflictChecks.SelectMany(c => c.Hits).Where(h => h.PersonId == person.Id).ToListAsync(cancellationToken))
        {
            hit.Redact(label);
        }

        var checks = await db.ConflictChecks
            .Where(c => (document != null && c.QueryDocument == document) || c.Hits.Any(h => h.PersonId == person.Id))
            .ToListAsync(cancellationToken);
        foreach (var check in checks.Where(c => (document != null && string.Equals(c.QueryDocument, document, StringComparison.Ordinal)) || string.Equals(SearchText.Normalize(c.QueryName), searchName, StringComparison.Ordinal)))
        {
            check.RedactQuery(label);
        }

        events.Publish(new PersonAnonymizedIntegrationEvent(person.Id));
        await db.SaveChangesAsync(cancellationToken);
        return person.ToResponse();
    }
}

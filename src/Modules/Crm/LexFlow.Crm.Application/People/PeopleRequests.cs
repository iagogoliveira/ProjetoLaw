using System.Text.Json.Nodes;
using FluentValidation;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.People;
using LexFlow.Platform.Contracts.CustomFields;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.People;

/// <summary>Cadastro enviado pela tela (criação e edição): dados, papéis, endereços, contatos e campos customizados.</summary>
public sealed record PersonInput(
    string Name,
    string? AlternateName,
    string? Document,
    string? Email,
    string? Phone,
    string? Notes,
    FiscalData? Fiscal,
    Guid? EconomicGroupId,
    IReadOnlyList<PersonRoleKind> Roles,
    IReadOnlyList<AddressInput>? Addresses,
    IReadOnlyList<ContactInput>? Contacts,
    JsonObject? CustomFields)
{
    public PersonDetails ToDetails() =>
        new(Name, AlternateName, Document, Email, Phone, Notes, Fiscal, EconomicGroupId, Roles, Addresses ?? [], Contacts ?? []);
}

public sealed record AddressResponse(string Street, string? Number, string? Complement, string? District, string City, string State, string PostalCode, bool IsBilling);

public sealed record ContactResponse(string Name, string? Department, string? Email, string? Phone, ContactChannel PreferredChannel);

public sealed record PersonResponse(
    Guid Id,
    PersonKind Kind,
    string Name,
    string? AlternateName,
    string? Document,
    string? Email,
    string? Phone,
    string? Notes,
    FiscalData Fiscal,
    Guid? EconomicGroupId,
    IReadOnlyList<PersonRoleKind> Roles,
    IReadOnlyList<AddressResponse> Addresses,
    IReadOnlyList<ContactResponse> Contacts,
    JsonObject CustomFields,
    DateTimeOffset CreatedAt);

/// <summary>Linha da lista de pessoas; <see cref="Score"/> é a semelhança com o termo buscado (0 a 1), quando houver.</summary>
public sealed record PersonSummary(Guid Id, PersonKind Kind, string Name, string? AlternateName, string? Document, IReadOnlyList<PersonRoleKind> Roles, double? Score);

internal static class PersonMapping
{
    public static PersonResponse ToResponse(this Person p) => new(
        p.Id,
        p.Kind,
        p.Name,
        p.AlternateName,
        p.Document,
        p.Email,
        p.Phone,
        p.Notes,
        p.Fiscal,
        p.EconomicGroupId,
        [.. p.Roles.Select(r => r.Role).Order()],
        [.. p.Addresses.Select(a => new AddressResponse(a.Street, a.Number, a.Complement, a.District, a.City, a.State, a.PostalCode, a.IsBilling))],
        [.. p.Contacts.Select(c => new ContactResponse(c.Name, c.Department, c.Email, c.Phone, c.PreferredChannel))],
        JsonNode.Parse(p.CustomFields)?.AsObject() ?? [],
        p.CreatedAt);
}

internal sealed class PersonInputValidator : AbstractValidator<PersonInput>
{
    public PersonInputValidator()
    {
        RuleFor(i => i.Name).NotEmpty().WithErrorCode("crm.name_required")
            .MaximumLength(Person.MaxNameLength).WithErrorCode("crm.name_too_long");
        RuleFor(i => i.AlternateName).MaximumLength(Person.MaxNameLength).WithErrorCode("crm.name_too_long");
        RuleFor(i => i.Email).Must(e => string.IsNullOrWhiteSpace(e) || Email.IsValid(e)).WithErrorCode("crm.email_invalid");
        RuleFor(i => i.Phone).Must(p => string.IsNullOrWhiteSpace(p) || LexFlow.SharedKernel.ValueObjects.Phone.IsValid(p)).WithErrorCode("crm.phone_invalid");
        RuleFor(i => i.Notes).MaximumLength(4000).WithErrorCode("crm.notes_too_long");
        RuleFor(i => i.Roles).NotEmpty().WithErrorCode(CrmErrors.RoleRequired.Code);
        RuleForEach(i => i.Roles).IsInEnum().WithErrorCode("crm.role_invalid");
        RuleForEach(i => i.Addresses).ChildRules(a =>
        {
            a.RuleFor(x => x.Street).NotEmpty().WithErrorCode("crm.street_required");
            a.RuleFor(x => x.City).NotEmpty().WithErrorCode("crm.city_required");
            a.RuleFor(x => x.State).Must(BrazilianState.IsValid).WithErrorCode(CrmErrors.InvalidState.Code);
            a.RuleFor(x => x.PostalCode).Must(Cep.IsValid).WithErrorCode(CrmErrors.InvalidPostalCode.Code);
        });
        RuleForEach(i => i.Contacts).ChildRules(c =>
        {
            c.RuleFor(x => x.Name).NotEmpty().WithErrorCode("crm.contact_name_required");
            c.RuleFor(x => x.Email).Must(e => string.IsNullOrWhiteSpace(e) || Email.IsValid(e)).WithErrorCode("crm.email_invalid");
        });
    }
}

public sealed record CreatePersonCommand(PersonKind Kind, PersonInput Person) : ICommand<PersonResponse>;

public sealed record UpdatePersonCommand(Guid Id, PersonInput Person) : ICommand<PersonResponse>;

internal sealed class CreatePersonValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RuleFor(c => c.Kind).IsInEnum().WithErrorCode("crm.kind_invalid");
        RuleFor(c => c.Person).NotNull().SetValidator(new PersonInputValidator());
    }
}

internal sealed class UpdatePersonValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonValidator() => RuleFor(c => c.Person).NotNull().SetValidator(new PersonInputValidator());
}

/// <summary>
/// Regras comuns à criação e à edição: documento único no escritório (critério de aceite da Etapa 4), grupo
/// econômico do escritório e campos customizados validados pelo módulo Platform.
/// </summary>
internal sealed class PersonRules(ICrmDbContext db, ICustomFieldValidator customFields)
{
    public async Task<Result<string>> CheckAsync(PersonKind kind, PersonInput input, Guid? personId, CancellationToken cancellationToken)
    {
        var document = Person.NormalizeDocument(kind, input.Document);
        if (document.IsFailure)
        {
            return document.Error;
        }

        if (document.Value is { } digits && await db.People.AnyAsync(p => p.Document == digits && p.Id != personId, cancellationToken))
        {
            return CrmErrors.DocumentTaken;
        }

        if (input.EconomicGroupId is { } group && !await db.EconomicGroups.AnyAsync(g => g.Id == group, cancellationToken))
        {
            return CrmErrors.EconomicGroupNotFound;
        }

        var validation = await customFields.ValidateAsync(CustomFieldEntities.People, input.CustomFields, cancellationToken);
        return validation.IsValid ? validation.Values.ToJsonString() : validation.ToValidationError();
    }
}

internal sealed class CreatePersonHandler(ICrmDbContext db, PersonRules rules, IIdGenerator ids) : ICommandHandler<CreatePersonCommand, PersonResponse>
{
    public async Task<Result<PersonResponse>> Handle(CreatePersonCommand command, CancellationToken cancellationToken)
    {
        var customFields = await rules.CheckAsync(command.Kind, command.Person, null, cancellationToken);
        if (customFields.IsFailure)
        {
            return customFields.Error;
        }

        var person = Person.Create(ids.NewId(), command.Kind, command.Person.ToDetails());
        if (person.IsFailure)
        {
            return person.Error;
        }

        person.Value.SetCustomFields(customFields.Value);
        db.People.Add(person.Value);
        await db.SaveChangesAsync(cancellationToken);
        return person.Value.ToResponse();
    }
}

internal sealed class UpdatePersonHandler(ICrmDbContext db, PersonRules rules) : ICommandHandler<UpdatePersonCommand, PersonResponse>
{
    public async Task<Result<PersonResponse>> Handle(UpdatePersonCommand command, CancellationToken cancellationToken)
    {
        var person = await db.People.Include(p => p.Roles).Include(p => p.Addresses).Include(p => p.Contacts)
            .SingleOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (person is null)
        {
            return CrmErrors.PersonNotFound;
        }

        var customFields = await rules.CheckAsync(person.Kind, command.Person, person.Id, cancellationToken);
        if (customFields.IsFailure)
        {
            return customFields.Error;
        }

        var updated = person.Update(command.Person.ToDetails());
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        person.SetCustomFields(customFields.Value);
        await db.SaveChangesAsync(cancellationToken);
        return person.ToResponse();
    }
}

/// <summary>Exclusão lógica: some das listas e libera o documento para um novo cadastro; o histórico fica na auditoria.</summary>
public sealed record DeletePersonCommand(Guid Id) : ICommand;

internal sealed class DeletePersonHandler(ICrmDbContext db) : ICommandHandler<DeletePersonCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeletePersonCommand command, CancellationToken cancellationToken)
    {
        var person = await db.People.SingleOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (person is null)
        {
            return CrmErrors.PersonNotFound;
        }

        db.People.Remove(person);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed record GetPersonQuery(Guid Id) : IQuery<PersonResponse>;

internal sealed class GetPersonHandler(ICrmDbContext db) : IQueryHandler<GetPersonQuery, PersonResponse>
{
    public async Task<Result<PersonResponse>> Handle(GetPersonQuery query, CancellationToken cancellationToken)
    {
        var person = await db.People.AsNoTracking().Include(p => p.Roles).Include(p => p.Addresses).Include(p => p.Contacts)
            .SingleOrDefaultAsync(p => p.Id == query.Id, cancellationToken);
        return person is null ? CrmErrors.PersonNotFound : person.ToResponse();
    }
}

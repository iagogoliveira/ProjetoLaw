using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Results;
using LexFlow.SharedKernel.ValueObjects;

namespace LexFlow.Crm.Domain.People;

/// <summary>Pessoa física (CPF) ou jurídica (CNPJ).</summary>
public enum PersonKind
{
    Individual,
    Company,
}

/// <summary>Papéis de uma pessoa no escritório (RF-06); a mesma pessoa pode ter vários.</summary>
public enum PersonRoleKind
{
    Client,
    Opponent,
    Witness,
    Expert,
    Correspondent,
    Supplier,
}

/// <summary>Dados fiscais para faturamento (RF-06): inscrições e retenções aplicáveis.</summary>
public sealed record FiscalData(
    string? StateRegistration = null,
    string? MunicipalRegistration = null,
    bool WithholdsIss = false,
    bool WithholdsIncomeTax = false,
    bool WithholdsPisCofinsCsll = false);

/// <summary>Endereço como informado no cadastro; o CEP e a UF são validados.</summary>
public sealed record AddressInput(
    string Street,
    string? Number,
    string? Complement,
    string? District,
    string City,
    string State,
    string PostalCode,
    bool IsBilling);

/// <summary>Contato da pessoa (financeiro, jurídico interno, diretoria…) com o canal preferido.</summary>
public sealed record ContactInput(string Name, string? Department, string? Email, string? Phone, ContactChannel PreferredChannel);

public enum ContactChannel
{
    Email,
    Phone,
    WhatsApp,
}

/// <summary>
/// Cadastro único de pessoas (RF-06): cliente, parte contrária, testemunha, perito, correspondente e fornecedor são
/// papéis da mesma pessoa. O documento (CPF da física, CNPJ da jurídica) é opcional (ex.: parte contrária sem
/// documento conhecido) e único no escritório. <see cref="SearchName"/> é o nome sem acentos e em minúsculas, base da
/// busca aproximada e da detecção de duplicidade.
/// </summary>
public sealed class Person : AggregateRoot<Guid>, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int MaxNameLength = 300;

    private readonly List<PersonRole> _roles = [];
    private readonly List<Address> _addresses = [];
    private readonly List<Contact> _contacts = [];

    private Person()
    {
    }

    private Person(Guid id, PersonKind kind)
        : base(id)
    {
        Kind = kind;
    }

    public Guid OrganizationId { get; private set; }

    public PersonKind Kind { get; private set; }

    /// <summary>Nome civil (física) ou razão social (jurídica).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Nome social (física) ou nome fantasia (jurídica).</summary>
    public string? AlternateName { get; private set; }

    public string SearchName { get; private set; } = string.Empty;

    /// <summary>Só dígitos (CPF ou CNPJ), conforme <see cref="Kind"/>.</summary>
    public string? Document { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Notes { get; private set; }

    public string? StateRegistration { get; private set; }

    public string? MunicipalRegistration { get; private set; }

    public bool WithholdsIss { get; private set; }

    public bool WithholdsIncomeTax { get; private set; }

    public bool WithholdsPisCofinsCsll { get; private set; }

    /// <summary>Dados fiscais agrupados (as colunas ficam na própria tabela).</summary>
    public FiscalData Fiscal => new(StateRegistration, MunicipalRegistration, WithholdsIss, WithholdsIncomeTax, WithholdsPisCofinsCsll);

    public Guid? EconomicGroupId { get; private set; }

    /// <summary>Valores dos campos customizados (JSON validado pelo módulo Platform).</summary>
    public string CustomFields { get; private set; } = "{}";

    public IReadOnlyCollection<PersonRole> Roles => _roles.AsReadOnly();

    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    public IReadOnlyCollection<Contact> Contacts => _contacts.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Documento normalizado (só dígitos) e conferido para o tipo de pessoa; nulo quando não informado.</summary>
    public static Result<string?> NormalizeDocument(PersonKind kind, string? document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return Result.Ok<string?>(null);
        }

        return kind == PersonKind.Individual
            ? Cpf.Create(document) is { IsSuccess: true } cpf ? cpf.Value.Value : CrmErrors.InvalidCpf
            : Cnpj.Create(document) is { IsSuccess: true } cnpj ? cnpj.Value.Value : CrmErrors.InvalidCnpj;
    }

    public static Result<Person> Create(Guid id, PersonKind kind, PersonDetails details)
    {
        var person = new Person(id, kind);
        var updated = person.Update(details);
        return updated.IsFailure ? updated.Error : person;
    }

    /// <summary>Substitui os dados do cadastro (a tela envia o cadastro inteiro). O tipo de pessoa não muda.</summary>
    public Result Update(PersonDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Name, nameof(details));
        var document = NormalizeDocument(Kind, details.Document);
        if (document.IsFailure)
        {
            return document.Error;
        }

        if (details.Roles.Count == 0)
        {
            return CrmErrors.RoleRequired;
        }

        var addresses = new List<Address>();
        foreach (var input in details.Addresses)
        {
            var address = Address.Create(Id, input);
            if (address.IsFailure)
            {
                return address.Error;
            }

            addresses.Add(address.Value);
        }

        Name = details.Name.Trim();
        AlternateName = Clean(details.AlternateName);
        SearchName = SearchText.Normalize($"{Name} {AlternateName}");
        Document = document.Value;
        Email = Clean(details.Email)?.ToLowerInvariant();
        Phone = Clean(details.Phone);
        Notes = Clean(details.Notes);
        var fiscal = details.Fiscal ?? new FiscalData();
        StateRegistration = Clean(fiscal.StateRegistration);
        MunicipalRegistration = Clean(fiscal.MunicipalRegistration);
        WithholdsIss = fiscal.WithholdsIss;
        WithholdsIncomeTax = fiscal.WithholdsIncomeTax;
        WithholdsPisCofinsCsll = fiscal.WithholdsPisCofinsCsll;
        EconomicGroupId = details.EconomicGroupId;

        _roles.RemoveAll(r => !details.Roles.Contains(r.Role));
        _roles.AddRange(details.Roles.Distinct().Where(r => !_roles.Exists(existing => existing.Role == r)).Select(r => new PersonRole(Id, r)));
        _addresses.Clear();
        _addresses.AddRange(addresses);
        _contacts.Clear();
        _contacts.AddRange(details.Contacts.Select(c => Contact.Create(Id, c)));
        return Result.Success;
    }

    /// <summary>Grava os campos customizados já validados (JSON de objeto).</summary>
    public void SetCustomFields(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        CustomFields = json;
    }

    public bool Has(PersonRoleKind role) => _roles.Exists(r => r.Role == role);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Dados editáveis do cadastro, sem o tipo (física ou jurídica), que é fixo.</summary>
public sealed record PersonDetails(
    string Name,
    string? AlternateName,
    string? Document,
    string? Email,
    string? Phone,
    string? Notes,
    FiscalData? Fiscal,
    Guid? EconomicGroupId,
    IReadOnlyCollection<PersonRoleKind> Roles,
    IReadOnlyCollection<AddressInput> Addresses,
    IReadOnlyCollection<ContactInput> Contacts);

/// <summary>Papel de uma pessoa (linha "pessoa × papel").</summary>
public sealed class PersonRole : ITenantOwned
{
    private PersonRole()
    {
    }

    internal PersonRole(Guid personId, PersonRoleKind role)
    {
        PersonId = personId;
        Role = role;
    }

    public Guid OrganizationId { get; private set; }

    public Guid PersonId { get; private set; }

    public PersonRoleKind Role { get; private set; }
}

public sealed class Address : ITenantOwned
{
    private Address()
    {
    }

    private Address(Guid personId, AddressInput input, string state, string postalCode)
    {
        Id = Guid.CreateVersion7();
        PersonId = personId;
        Street = input.Street.Trim();
        Number = input.Number?.Trim();
        Complement = input.Complement?.Trim();
        District = input.District?.Trim();
        City = input.City.Trim();
        State = state;
        PostalCode = postalCode;
        IsBilling = input.IsBilling;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PersonId { get; private set; }

    public string Street { get; private set; } = string.Empty;

    public string? Number { get; private set; }

    public string? Complement { get; private set; }

    public string? District { get; private set; }

    public string City { get; private set; } = string.Empty;

    /// <summary>UF (ex.: <c>SP</c>).</summary>
    public string State { get; private set; } = string.Empty;

    /// <summary>CEP só com dígitos.</summary>
    public string PostalCode { get; private set; } = string.Empty;

    /// <summary>Endereço de cobrança (faturamento).</summary>
    public bool IsBilling { get; private set; }

    internal static Result<Address> Create(Guid personId, AddressInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Street, nameof(input));
        ArgumentException.ThrowIfNullOrWhiteSpace(input.City, nameof(input));
        var state = input.State?.Trim().ToUpperInvariant();
        if (!BrazilianState.IsValid(state))
        {
            return CrmErrors.InvalidState;
        }

        var postalCode = Cep.Create(input.PostalCode);
        return postalCode.IsFailure ? CrmErrors.InvalidPostalCode : new Address(personId, input, state!, postalCode.Value.Value);
    }
}

public sealed class Contact : ITenantOwned
{
    private Contact()
    {
    }

    private Contact(Guid personId, ContactInput input)
    {
        Id = Guid.CreateVersion7();
        PersonId = personId;
        Name = input.Name.Trim();
        Department = input.Department?.Trim();
        Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant();
        Phone = input.Phone?.Trim();
        PreferredChannel = input.PreferredChannel;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public Guid PersonId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Área do contato no cliente (financeiro, jurídico interno, diretoria…).</summary>
    public string? Department { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public ContactChannel PreferredChannel { get; private set; }

    internal static Contact Create(Guid personId, ContactInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Name, nameof(input));
        return new Contact(personId, input);
    }
}

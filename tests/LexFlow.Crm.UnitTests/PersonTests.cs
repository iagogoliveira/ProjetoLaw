using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.People;

namespace LexFlow.Crm.UnitTests;

public sealed class PersonTests
{
    private static PersonDetails Details(
        string name = "João da Silva",
        string? document = null,
        IReadOnlyCollection<PersonRoleKind>? roles = null,
        IReadOnlyCollection<AddressInput>? addresses = null) =>
        new(name, null, document, " Joao@Exemplo.com.br ", null, null, null, null, roles ?? [PersonRoleKind.Client], addresses ?? [], []);

    [Theory]
    [InlineData("João da Silva Ltda.", "joao da silva ltda")]
    [InlineData("  JOSÉ   d'Ávila\tME ", "jose d avila me")]
    [InlineData("Ação & Cia", "acao cia")]
    [InlineData("", "")]
    public void Search_text_drops_accents_case_and_punctuation(string text, string expected) =>
        SearchText.Normalize(text).ShouldBe(expected);

    [Fact]
    public void Documents_are_checked_for_the_kind_of_person_and_kept_as_digits()
    {
        Person.NormalizeDocument(PersonKind.Individual, "529.982.247-25").Value.ShouldBe("52998224725");
        Person.NormalizeDocument(PersonKind.Company, "11.222.333/0001-81").Value.ShouldBe("11222333000181");
        Person.NormalizeDocument(PersonKind.Individual, "11.222.333/0001-81").Error.ShouldBe(CrmErrors.InvalidCpf);
        Person.NormalizeDocument(PersonKind.Company, "529.982.247-25").Error.ShouldBe(CrmErrors.InvalidCnpj);
        Person.NormalizeDocument(PersonKind.Company, "  ").Value.ShouldBeNull();
    }

    [Fact]
    public void Person_keeps_roles_addresses_contacts_and_the_search_name()
    {
        var person = Person.Create(Guid.CreateVersion7(), PersonKind.Individual, Details(
            document: "529.982.247-25",
            roles: [PersonRoleKind.Client, PersonRoleKind.Client, PersonRoleKind.Witness],
            addresses: [new AddressInput(" Rua A ", "10", null, "Centro", "São Paulo", "sp", "01310-100", IsBilling: true)])).Value;

        person.Name.ShouldBe("João da Silva");
        person.SearchName.ShouldBe("joao da silva");
        person.Document.ShouldBe("52998224725");
        person.Email.ShouldBe("joao@exemplo.com.br");
        person.Roles.Select(r => r.Role).ShouldBe([PersonRoleKind.Client, PersonRoleKind.Witness], ignoreOrder: true);
        person.Addresses.Single().State.ShouldBe("SP");
        person.Addresses.Single().PostalCode.ShouldBe("01310100");
        person.Has(PersonRoleKind.Opponent).ShouldBeFalse();
        person.CustomFields.ShouldBe("{}");
    }

    [Fact]
    public void Update_replaces_the_record_but_refuses_invalid_data()
    {
        var person = Person.Create(Guid.CreateVersion7(), PersonKind.Company, Details("Empresa Antiga")).Value;

        person.Update(Details("Empresa Nova S.A.", roles: [PersonRoleKind.Opponent]) with
        {
            AlternateName = "Nova",
            Fiscal = new FiscalData("123", "456", WithholdsIss: true),
        }).IsSuccess.ShouldBeTrue();

        person.SearchName.ShouldBe("empresa nova s a nova");
        person.Roles.Select(r => r.Role).ShouldBe([PersonRoleKind.Opponent]);
        person.Fiscal.ShouldBe(new FiscalData("123", "456", WithholdsIss: true));
        person.Update(Details(roles: [])).Error.ShouldBe(CrmErrors.RoleRequired);
        person.Update(Details(addresses: [new AddressInput("Rua", null, null, null, "Cidade", "XX", "01310100", false)])).Error.ShouldBe(CrmErrors.InvalidState);
        person.Update(Details(addresses: [new AddressInput("Rua", null, null, null, "Cidade", "SP", "123", false)])).Error.ShouldBe(CrmErrors.InvalidPostalCode);
        person.Update(Details(document: "000")).Error.ShouldBe(CrmErrors.InvalidCnpj);
        person.Name.ShouldBe("Empresa Nova S.A.", "uma edição recusada não muda nada");
    }

    [Fact]
    public void Economic_group_names_are_trimmed()
    {
        var group = EconomicGroup.Create(Guid.CreateVersion7(), " Grupo Silva ");
        group.Rename(" Grupo Silva & Filhos ");

        group.Name.ShouldBe("Grupo Silva & Filhos");
    }
}

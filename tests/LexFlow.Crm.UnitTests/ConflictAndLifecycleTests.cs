using System.Text.Json.Nodes;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Conflicts;
using LexFlow.Crm.Domain.People;

namespace LexFlow.Crm.UnitTests;

public sealed class ConflictAndLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static Person Company(string name, string? document = null, PersonRoleKind role = PersonRoleKind.Client, string? email = null, IReadOnlyCollection<ContactInput>? contacts = null, IReadOnlyCollection<AddressInput>? addresses = null) =>
        Person.Create(Guid.CreateVersion7(), PersonKind.Company, new PersonDetails(name, null, document, email, null, null, null, null, [role], addresses ?? [], contacts ?? [])).Value;

    private static AddressInput Address(string street) => new(street, "10", null, null, "São Paulo", "SP", "01310100", false);

    [Fact]
    public void A_check_takes_the_highest_risk_of_its_hits()
    {
        var none = ConflictCheck.Record(Guid.CreateVersion7(), "Ninguém", null, ConflictSide.Client, []);
        var direct = ConflictCheck.Record(Guid.CreateVersion7(), " Empresa X ", "11222333000181", ConflictSide.Opponent,
        [
            new ConflictHitInput(ConflictRisk.Indirect, ConflictReason.SimilarName, Guid.CreateVersion7(), "Empresa Xis", null, 0.7),
            new ConflictHitInput(ConflictRisk.Direct, ConflictReason.SameDocument, Guid.CreateVersion7(), "Empresa X", null, null),
        ]);

        none.Risk.ShouldBe(ConflictRisk.None);
        direct.Risk.ShouldBe(ConflictRisk.Direct);
        direct.QueryName.ShouldBe("Empresa X");
        direct.Hits.Count.ShouldBe(2);
    }

    [Fact]
    public void Proceeding_with_a_conflict_requires_a_justification_and_happens_once()
    {
        var check = ConflictCheck.Record(Guid.CreateVersion7(), "Empresa X", null, ConflictSide.Client,
            [new ConflictHitInput(ConflictRisk.Indirect, ConflictReason.SameEconomicGroup, Guid.CreateVersion7(), "Empresa Y", null, null)]);
        var clean = ConflictCheck.Record(Guid.CreateVersion7(), "Ninguém", null, ConflictSide.Client, []);

        check.Proceed("  ", Now).Error.ShouldBe(CrmErrors.ConflictJustificationRequired);
        check.Proceed(" Grupos distintos desde 2025 ", Now).IsSuccess.ShouldBeTrue();
        check.Proceed("de novo", Now).Error.ShouldBe(CrmErrors.ConflictAlreadyDecided);
        clean.Proceed(null, Now).IsSuccess.ShouldBeTrue();

        check.Justification.ShouldBe("Grupos distintos desde 2025");
        check.ProceededAt.ShouldBe(Now);
        clean.Justification.ShouldBeNull();
    }

    [Fact]
    public void Merging_keeps_the_record_fills_the_blanks_and_sums_roles_addresses_and_contacts()
    {
        var keep = Company("Horizonte S.A.", addresses: [Address("Rua A")], contacts: [new ContactInput("Ana", "Financeiro", "ana@x.com.br", null, ContactChannel.Email)]);
        var duplicate = Company("Horizonte SA", "11222333000181", PersonRoleKind.Opponent, "contato@horizonte.com.br",
            [new ContactInput("Ana Paula", null, "ana@x.com.br", null, ContactChannel.Phone), new ContactInput("Bruno", "Jurídico", "bruno@x.com.br", null, ContactChannel.Email)],
            [Address("Rua A"), Address("Rua B")]);
        keep.SetCustomFields("""{"origem":"Indicação"}""");
        duplicate.SetCustomFields("""{"origem":"Site","segmento":"Construção"}""");

        var adopted = keep.Absorb(duplicate);

        adopted.Value.ShouldBe("11222333000181");
        keep.Document.ShouldBeNull("o documento só é adotado depois de o duplicado soltá-lo");
        keep.AdoptDocument(adopted.Value!);
        keep.Document.ShouldBe("11222333000181");
        keep.Name.ShouldBe("Horizonte S.A.");
        keep.Email.ShouldBe("contato@horizonte.com.br");
        keep.Roles.Select(r => r.Role).ShouldBe([PersonRoleKind.Client, PersonRoleKind.Opponent], ignoreOrder: true);
        keep.Addresses.Select(a => a.Street).ShouldBe(["Rua A", "Rua B"]);
        keep.Contacts.Select(c => c.Name).ShouldBe(["Ana", "Bruno"]);
        var fields = JsonNode.Parse(keep.CustomFields)!.AsObject();
        fields["origem"]!.GetValue<string>().ShouldBe("Indicação");
        fields["segmento"]!.GetValue<string>().ShouldBe("Construção");
        duplicate.MergedIntoId.ShouldBe(keep.Id);
        duplicate.Document.ShouldBeNull();
    }

    [Fact]
    public void Merging_refuses_different_kinds_documents_or_the_same_record()
    {
        var company = Company("Empresa", "11222333000181");
        var other = Company("Outra", "11444777000161");
        var individual = Person.Create(Guid.CreateVersion7(), PersonKind.Individual, new PersonDetails("João", null, null, null, null, null, null, null, [PersonRoleKind.Client], [], [])).Value;

        company.Absorb(company).Error.ShouldBe(CrmErrors.MergeSamePerson);
        company.Absorb(individual).Error.ShouldBe(CrmErrors.MergeKindMismatch);
        company.Absorb(other).Error.ShouldBe(CrmErrors.MergeDocumentConflict);
    }

    [Fact]
    public void Anonymizing_removes_personal_data_keeps_the_record_and_cannot_be_undone()
    {
        var person = Company("João da Silva Ltda.", "11222333000181", email: "joao@x.com.br", contacts: [new ContactInput("Ana", null, "ana@x.com.br", null, ContactChannel.Email)], addresses: [Address("Rua A")]);
        person.SetCustomFields("""{"origem":"Site"}""");

        person.Anonymize(" Pedido do titular ", Now).IsSuccess.ShouldBeTrue();

        person.Name.ShouldBe(Person.AnonymizedName(person.Id));
        person.SearchName.ShouldStartWith("pessoa anonimizada");
        person.Document.ShouldBeNull();
        person.Email.ShouldBeNull();
        person.Addresses.ShouldBeEmpty();
        person.Contacts.ShouldBeEmpty();
        person.CustomFields.ShouldBe("{}");
        person.Roles.Select(r => r.Role).ShouldBe([PersonRoleKind.Client]);
        person.AnonymizationReason.ShouldBe("Pedido do titular");
        person.IsAnonymized.ShouldBeTrue();
        person.Anonymize("de novo", Now).Error.ShouldBe(CrmErrors.PersonAnonymized);
        person.Update(new PersonDetails("Volta", null, null, null, null, null, null, null, [PersonRoleKind.Client], [], [])).Error.ShouldBe(CrmErrors.PersonAnonymized);
        Company("Outra").Absorb(person).Error.ShouldBe(CrmErrors.PersonAnonymized);
    }

    [Fact]
    public void Hits_follow_merges_and_lose_the_name_on_anonymization()
    {
        var check = ConflictCheck.Record(Guid.CreateVersion7(), "Empresa X", "11222333000181", ConflictSide.Client,
            [new ConflictHitInput(ConflictRisk.Direct, ConflictReason.SameDocument, Guid.CreateVersion7(), "Empresa X", null, null)]);
        var hit = check.Hits.Single();
        var kept = Guid.CreateVersion7();

        hit.PointTo(kept, "Empresa X S.A.");
        hit.Redact("Pessoa anonimizada");
        check.RedactQuery("Pessoa anonimizada");

        hit.PersonId.ShouldBe(kept);
        hit.MatchedName.ShouldBe("Pessoa anonimizada");
        check.QueryName.ShouldBe("Pessoa anonimizada");
        check.QueryDocument.ShouldBeNull();
    }
}

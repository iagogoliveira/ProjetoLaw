using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Domain.Numbering;

namespace LexFlow.Litigation.UnitTests;

public sealed class LegalCaseTests
{
    /// <summary>Número CNJ válido (TJSP), com o dígito verificador conferido.</summary>
    private const string Cnj = "2238058-98.2025.8.26.8434";

    private static readonly Guid Client = Guid.CreateVersion7();
    private static readonly Guid Opponent = Guid.CreateVersion7();

    private static CaseDetails Details(string? cnj = Cnj, IReadOnlyList<PartyInput>? parties = null, Guid? payer = null, IReadOnlyList<AssignmentInput>? assignments = null) => new(
        cnj, null, CaseInstance.First, " Foro Central ", null, null, "Procedimento Comum", null, null, null, null, null, 150_000.555m,
        new DateOnly(2026, 1, 10), null, null, payer,
        parties ?? [new PartyInput(Client, PartyPole.Active, true, null, null), new PartyInput(Opponent, PartyPole.Passive, false, " Dra. Ana ", "sp 123456")],
        assignments ?? []);

    private static LegalCase Judicial() => LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "TRAB-2026-00001", Details()).Value;

    [Fact]
    public void A_judicial_case_needs_a_valid_cnj_and_the_others_have_none()
    {
        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(cnj: null)).Error.ShouldBe(LitigationErrors.CnjRequired);
        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(cnj: "2238058-99.2025.8.26.8434")).Error.ShouldBe(LitigationErrors.CnjInvalid);
        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Administrative, "X", Details()).Error.ShouldBe(LitigationErrors.CnjNotAllowed);

        var judicial = Judicial();
        judicial.CnjNumber.ShouldBe("22380589820258268434");
        judicial.District.ShouldBe("Foro Central");
        judicial.ClaimValue.ShouldBe(150_000.56m);
        judicial.Status.ShouldBe(CaseStatus.Active);
        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Extrajudicial, "X", Details(cnj: null)).Value.CnjNumber.ShouldBeNull();
    }

    [Fact]
    public void Parties_need_a_client_without_repeating_a_person_and_the_payer_is_a_client()
    {
        var onlyOpponent = new[] { new PartyInput(Opponent, PartyPole.Passive, false, null, null) };
        var repeated = new[] { new PartyInput(Client, PartyPole.Active, true, null, null), new PartyInput(Client, PartyPole.Passive, false, null, null) };

        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(parties: onlyOpponent)).Error.ShouldBe(LitigationErrors.ClientRequired);
        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(parties: repeated)).Error.ShouldBe(LitigationErrors.PartyDuplicated);
        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(payer: Opponent)).Error.ShouldBe(LitigationErrors.PayerNotClient);

        var opposing = Judicial().Parties.Single(p => !p.IsClient);
        opposing.CounselName.ShouldBe("Dra. Ana");
        opposing.CounselOab.ShouldBe("SP 123456");
    }

    [Fact]
    public void A_partner_or_lawyer_is_a_user_and_a_team_is_a_team()
    {
        var user = Guid.CreateVersion7();
        var team = Guid.CreateVersion7();

        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(assignments: [new AssignmentInput(AssignmentRole.Lawyer, null, team)]))
            .Error.ShouldBe(LitigationErrors.AssignmentInvalid);
        LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(assignments: [new AssignmentInput(AssignmentRole.Team, user, null)]))
            .Error.ShouldBe(LitigationErrors.AssignmentInvalid);

        var legalCase = LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(assignments:
        [
            new AssignmentInput(AssignmentRole.Lawyer, user, null),
            new AssignmentInput(AssignmentRole.Lawyer, user, null),
            new AssignmentInput(AssignmentRole.Team, null, team),
        ])).Value;
        legalCase.Assignments.Count.ShouldBe(2);
    }

    [Fact]
    public void A_closed_case_does_not_change_until_reopened()
    {
        var legalCase = Judicial();
        var reason = Guid.CreateVersion7();

        legalCase.Close(new ClosingInput(reason, CaseOutcome.Settlement, -1m, new DateOnly(2026, 9, 1), null)).Error.ShouldBe(LitigationErrors.ValueInvalid);
        legalCase.Close(new ClosingInput(reason, CaseOutcome.Settlement, 40_000m, new DateOnly(2026, 9, 1), " Acordo homologado ")).IsSuccess.ShouldBeTrue();
        legalCase.Status.ShouldBe(CaseStatus.Closed);
        legalCase.ClosingNote.ShouldBe("Acordo homologado");
        legalCase.Close(new ClosingInput(reason, CaseOutcome.Denied, null, new DateOnly(2026, 9, 2), null)).Error.ShouldBe(LitigationErrors.CaseClosed);
        legalCase.Update(Details()).Error.ShouldBe(LitigationErrors.CaseClosed);
        legalCase.SetStatus(CaseStatus.Suspended).Error.ShouldBe(LitigationErrors.CaseClosed);

        legalCase.Reopen().IsSuccess.ShouldBeTrue();
        legalCase.Status.ShouldBe(CaseStatus.Active);
        legalCase.Outcome.ShouldBeNull();
        legalCase.FinalAmount.ShouldBeNull();
        legalCase.Reopen().Error.ShouldBe(LitigationErrors.CaseNotClosed);
        legalCase.SetStatus(CaseStatus.Suspended).IsSuccess.ShouldBeTrue();
        legalCase.SetStatus(CaseStatus.Closed).Error.ShouldBe(LitigationErrors.CaseClosed);
    }

    [Fact]
    public void Merging_people_points_the_parties_and_the_payer_to_the_kept_record()
    {
        var kept = Guid.CreateVersion7();
        var legalCase = LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "X", Details(payer: Client)).Value;
        var both = LegalCase.Create(Guid.CreateVersion7(), CaseKind.Judicial, "Y", Details(parties:
        [
            new PartyInput(Client, PartyPole.Active, true, null, null),
            new PartyInput(kept, PartyPole.Active, true, null, null),
        ])).Value;

        legalCase.ReplacePerson(Client, kept);
        both.ReplacePerson(Client, kept);

        legalCase.Parties.Select(p => p.PersonId).ShouldBe([kept, Opponent], ignoreOrder: true);
        legalCase.PayerPersonId.ShouldBe(kept);
        both.Parties.Select(p => p.PersonId).ShouldBe([kept], "a mesma pessoa não aparece duas vezes");
    }
}

public sealed class CaseNumberingTests
{
    private static readonly Guid Labor = Guid.CreateVersion7();

    [Fact]
    public void The_default_format_uses_the_area_code_the_year_and_a_padded_sequence()
    {
        var numbering = CaseNumbering.Default(Guid.CreateVersion7());

        numbering.Render(numbering.CodeOf(Labor, "Trabalhista"), 2026, 7).ShouldBe("TRAB-2026-00007");
        numbering.CodeOf(Guid.CreateVersion7(), "Cível").ShouldBe("CIVE");
        numbering.CodeOf(null, null).ShouldBe(CaseNumbering.NoAreaCode);
        numbering.CodeOf(Guid.CreateVersion7(), "—").ShouldBe(CaseNumbering.NoAreaCode);
    }

    [Fact]
    public void The_office_sets_its_own_format_and_area_codes()
    {
        var numbering = CaseNumbering.Default(Guid.CreateVersion7());

        numbering.Configure("{ANO}/{AREA}/{SEQ:000}", new Dictionary<Guid, string> { [Labor] = " trab1 " }).IsSuccess.ShouldBeTrue();

        numbering.CodeOf(Labor, "Trabalhista").ShouldBe("TRAB1");
        numbering.Render("TRAB1", 2026, 12).ShouldBe("2026/TRAB1/012");
        numbering.Configure("{SEQ}", new Dictionary<Guid, string>()).IsSuccess.ShouldBeTrue();
        numbering.Render("X", 2026, 1234).ShouldBe("1234");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{AREA}-{ANO}")]
    [InlineData("{SEQ}-{SEQ}")]
    [InlineData("{CLIENTE}-{SEQ}")]
    [InlineData("{SEQ}-{ano")]
    public void A_format_needs_exactly_one_sequence_and_only_known_markers(string format) =>
        CaseNumbering.Default(Guid.CreateVersion7()).Configure(format, new Dictionary<Guid, string>()).Error.ShouldBe(LitigationErrors.NumberingFormatInvalid);

    [Fact]
    public void An_area_code_has_one_to_ten_letters_or_digits() =>
        CaseNumbering.Default(Guid.CreateVersion7()).Configure(CaseNumbering.DefaultFormat, new Dictionary<Guid, string> { [Labor] = "TRAB-X" })
            .Error.ShouldBe(LitigationErrors.AreaCodeInvalid);
}

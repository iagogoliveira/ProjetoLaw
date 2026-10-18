using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Hearings;
using LexFlow.Litigation.Domain.Risk;
using LexFlow.Litigation.Domain.Settlements;

namespace LexFlow.Litigation.UnitTests;

/// <summary>Risco, acordos e audiências (PR 5C2).</summary>
public sealed class FollowUpTests
{
    private static readonly Guid CaseId = Guid.CreateVersion7();
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Hearing1 = new(2026, 11, 10, 14, 0, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(RiskProbability.Probable, RiskAccounting.Provision)]
    [InlineData(RiskProbability.Possible, RiskAccounting.ContingentLiability)]
    [InlineData(RiskProbability.Remote, RiskAccounting.None)]
    public void Each_probability_has_its_accounting_treatment(RiskProbability probability, RiskAccounting accounting)
    {
        var assessment = RiskAssessment.Record(Guid.CreateVersion7(), CaseId, new RiskInput(null, probability, 10_000.555m, "  Prova testemunhal fraca  "), User, Now).Value;

        assessment.Accounting.ShouldBe(accounting);
        assessment.EstimatedLoss.ShouldBe(10_000.56m);
        assessment.Rationale.ShouldBe("Prova testemunhal fraca");
        assessment.LegalCaseId.ShouldBe(CaseId);
        assessment.AssessedBy.ShouldBe(User);
        RiskAssessment.Record(Guid.CreateVersion7(), CaseId, new RiskInput(null, probability, -1m, null), User, Now).Error.ShouldBe(LitigationErrors.ValueInvalid);
    }

    [Fact]
    public void A_settlement_goes_from_negotiation_to_fulfilled()
    {
        var settlement = Settlement.Start(Guid.CreateVersion7(), CaseId, SettlementDirection.ClientPays, " Proposta inicial ", new OfferInput(OfferParty.Counterparty, 80_000m, "à vista", new DateOnly(2026, 9, 1))).Value;
        settlement.AddOffer(new OfferInput(OfferParty.Office, 30_000m, null, new DateOnly(2026, 9, 10))).IsSuccess.ShouldBeTrue();
        settlement.AddOffer(new OfferInput(OfferParty.Counterparty, 50_000m, "em duas vezes", new DateOnly(2026, 9, 20))).IsSuccess.ShouldBeTrue();

        settlement.Submit(50_000m, [new InstallmentInput(new DateOnly(2026, 12, 10), 25_000m), new InstallmentInput(new DateOnly(2026, 11, 10), 25_000m)], User, Now).IsSuccess.ShouldBeTrue();
        settlement.AddOffer(new OfferInput(OfferParty.Office, 1m, null, new DateOnly(2026, 9, 21))).Error.ShouldBe(LitigationErrors.SettlementNotNegotiating);
        settlement.Homologate(new DateOnly(2026, 10, 1)).Error.ShouldBe(LitigationErrors.SettlementNotApproved);
        settlement.Approve(User, Now).IsSuccess.ShouldBeTrue();
        settlement.Homologate(new DateOnly(2026, 10, 1)).IsSuccess.ShouldBeTrue();

        settlement.Offers.Select(o => o.Round).ShouldBe([1, 2, 3]);
        settlement.Notes.ShouldBe("Proposta inicial");
        var installments = settlement.Installments.OrderBy(i => i.Number).ToList();
        installments.Select(i => i.DueOn).ShouldBe([new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10)]);
        installments[0].IsOverdue(new DateOnly(2026, 11, 11)).ShouldBeTrue();
        installments[0].IsOverdue(new DateOnly(2026, 11, 10)).ShouldBeFalse();

        settlement.PayInstallment(installments[0].Id, new DateOnly(2026, 11, 10), 25_000m).IsSuccess.ShouldBeTrue();
        settlement.PayInstallment(installments[0].Id, new DateOnly(2026, 11, 10), 25_000m).Error.ShouldBe(LitigationErrors.InstallmentAlreadyPaid);
        settlement.Status.ShouldBe(SettlementStatus.Homologated);
        settlement.PayInstallment(installments[1].Id, new DateOnly(2026, 12, 9), 25_000m).IsSuccess.ShouldBeTrue();

        settlement.Status.ShouldBe(SettlementStatus.Fulfilled);
        installments[0].IsOverdue(new DateOnly(2027, 1, 1)).ShouldBeFalse();
        settlement.Cancel("tarde").Error.ShouldBe(LitigationErrors.SettlementClosed);
    }

    [Fact]
    public void The_terms_need_installments_that_add_up_to_the_agreed_amount()
    {
        var settlement = Settlement.Start(Guid.CreateVersion7(), CaseId, SettlementDirection.ClientReceives, null, null).Value;

        settlement.Submit(10_000m, [], User, Now).Error.ShouldBe(LitigationErrors.SettlementInstallmentsMismatch);
        settlement.Submit(10_000m, [new InstallmentInput(new DateOnly(2026, 11, 1), 9_999.99m)], User, Now).Error.ShouldBe(LitigationErrors.SettlementInstallmentsMismatch);
        settlement.Submit(0m, [new InstallmentInput(new DateOnly(2026, 11, 1), 0m)], User, Now).Error.ShouldBe(LitigationErrors.ValueInvalid);
        settlement.AddOffer(new OfferInput(OfferParty.Office, -1m, null, new DateOnly(2026, 9, 1))).Error.ShouldBe(LitigationErrors.ValueInvalid);
        Settlement.Start(Guid.CreateVersion7(), CaseId, SettlementDirection.ClientPays, null, new OfferInput(OfferParty.Office, -1m, null, new DateOnly(2026, 9, 1)))
            .Error.ShouldBe(LitigationErrors.ValueInvalid);
        settlement.PayInstallment(Guid.CreateVersion7(), new DateOnly(2026, 11, 1), 1m).Error.ShouldBe(LitigationErrors.SettlementNotHomologated);
        settlement.Status.ShouldBe(SettlementStatus.Negotiating);
    }

    [Fact]
    public void Returned_terms_go_back_to_negotiation_and_ended_negotiations_take_no_changes()
    {
        var settlement = Settlement.Start(Guid.CreateVersion7(), CaseId, SettlementDirection.ClientPays, null, null).Value;
        settlement.ReturnToNegotiation("x").Error.ShouldBe(LitigationErrors.SettlementNotAwaitingApproval);
        settlement.Approve(User, Now).Error.ShouldBe(LitigationErrors.SettlementNotAwaitingApproval);
        settlement.Submit(100m, [new InstallmentInput(new DateOnly(2026, 11, 1), 100m)], User, Now);
        settlement.Approve(User, Now);

        settlement.ReturnToNegotiation(" valor alto ").IsSuccess.ShouldBeTrue();
        settlement.Status.ShouldBe(SettlementStatus.Negotiating);
        settlement.ApprovedBy.ShouldBeNull();
        settlement.Reason.ShouldBe("valor alto");

        settlement.Submit(90m, [new InstallmentInput(new DateOnly(2026, 11, 1), 90m)], User, Now).IsSuccess.ShouldBeTrue();
        settlement.Installments.Single().Amount.ShouldBe(90m);
        settlement.Reason.ShouldBeNull();
        settlement.Reject("sem acordo").IsSuccess.ShouldBeTrue();
        settlement.Status.ShouldBe(SettlementStatus.Rejected);
        settlement.IsOpen.ShouldBeFalse();
        settlement.Reject("de novo").Error.ShouldBe(LitigationErrors.SettlementClosed);

        var homologated = Settlement.Start(Guid.CreateVersion7(), CaseId, SettlementDirection.ClientPays, null, null).Value;
        homologated.Submit(1m, [new InstallmentInput(new DateOnly(2026, 11, 1), 1m)], User, Now);
        homologated.Approve(User, Now);
        homologated.Homologate(new DateOnly(2026, 10, 1));
        homologated.PayInstallment(Guid.CreateVersion7(), new DateOnly(2026, 11, 1), 1m).Error.ShouldBe(LitigationErrors.InstallmentNotFound);
        homologated.PayInstallment(homologated.Installments.Single().Id, new DateOnly(2026, 11, 1), 0m).Error.ShouldBe(LitigationErrors.ValueInvalid);
        homologated.Cancel(null).Error.ShouldBe(LitigationErrors.SettlementClosed);
    }

    private static HearingInput Input(HearingFormat format = HearingFormat.Hybrid, string? location = " Fórum Trabalhista, sala 3 ", string? link = "https://meet.example.com/abc") =>
        new(HearingType.Instruction, format, Hearing1, location, link, User, " Maria Preposta ", [Guid.Empty, Guid.Empty], null);

    [Fact]
    public void A_hearing_needs_the_place_its_format_asks_for()
    {
        Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input(HearingFormat.InPerson, location: null)).Error.ShouldBe(LitigationErrors.HearingPlaceRequired);
        Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input(HearingFormat.Virtual, link: " ")).Error.ShouldBe(LitigationErrors.HearingPlaceRequired);
        Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input(HearingFormat.Hybrid, link: null)).Error.ShouldBe(LitigationErrors.HearingPlaceRequired);
        Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input(HearingFormat.Virtual, link: "ftp://x")).Error.ShouldBe(LitigationErrors.HearingLinkInvalid);

        var virtualOnly = Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input(HearingFormat.Virtual)).Value;
        virtualOnly.Location.ShouldBeNull();
        virtualOnly.MeetingLink.ShouldBe("https://meet.example.com/abc");

        var hybrid = Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input()).Value;
        hybrid.Location.ShouldBe("Fórum Trabalhista, sala 3");
        hybrid.Representative.ShouldBe("Maria Preposta");
        hybrid.WitnessPersonIds.Count.ShouldBe(1);
        hybrid.Status.ShouldBe(HearingStatus.Scheduled);
    }

    [Fact]
    public void Rescheduling_keeps_the_same_hearing_with_the_previous_dates()
    {
        var hearing = Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input()).Value;
        var later = Hearing1.AddDays(14);

        hearing.Reschedule(Hearing1, "x", User, Now).Error.ShouldBe(LitigationErrors.HearingSameDate);
        hearing.Reschedule(later, " pedido do juízo ", User, Now).IsSuccess.ShouldBeTrue();
        hearing.Update(Input(HearingFormat.InPerson) with { ScheduledAt = Hearing1.AddYears(1) }).IsSuccess.ShouldBeTrue();

        hearing.ScheduledAt.ShouldBe(later);
        hearing.Format.ShouldBe(HearingFormat.InPerson);
        hearing.MeetingLink.ShouldBeNull();
        var change = hearing.Reschedules.Single();
        (change.PreviousDate, change.NewDate, change.Reason, change.ChangedBy).ShouldBe((Hearing1, later, "pedido do juízo", User));
    }

    [Fact]
    public void A_held_or_cancelled_hearing_does_not_change()
    {
        var held = Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input()).Value;
        var cancelled = Hearing.Schedule(Guid.CreateVersion7(), CaseId, Input()).Value;

        held.Hold(" Acordo proposto, sem conciliação ").IsSuccess.ShouldBeTrue();
        cancelled.Cancel("Juiz em férias").IsSuccess.ShouldBeTrue();

        held.Status.ShouldBe(HearingStatus.Held);
        held.Outcome.ShouldBe("Acordo proposto, sem conciliação");
        cancelled.Status.ShouldBe(HearingStatus.Cancelled);
        held.Hold("de novo").Error.ShouldBe(LitigationErrors.HearingClosed);
        held.Update(Input()).Error.ShouldBe(LitigationErrors.HearingClosed);
        cancelled.Reschedule(Hearing1.AddDays(1), null, User, Now).Error.ShouldBe(LitigationErrors.HearingClosed);
    }
}

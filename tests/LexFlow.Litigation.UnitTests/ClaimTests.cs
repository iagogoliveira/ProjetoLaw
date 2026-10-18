using LexFlow.Litigation.Domain;
using LexFlow.Litigation.Domain.Cases;
using LexFlow.Litigation.Domain.Claims;

namespace LexFlow.Litigation.UnitTests;

public sealed class ClaimTests
{
    private static readonly Guid CaseId = Guid.CreateVersion7();
    private static readonly Guid Overtime = Guid.CreateVersion7();
    private static readonly Guid Author = Guid.CreateVersion7();
    private static readonly Guid Editor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Created = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly CorrectionSettings Ipca = new(ClaimCorrectionIndex.IpcaE, new DateOnly(2024, 1, 10), new DateOnly(2024, 2, 5), 1m);

    private static ClaimInput Input(IReadOnlyList<ValuationInput>? valuations = null, CorrectionSettings? correction = null, string? description = " Horas extras de 2023 ") =>
        new(Overtime, description, correction ?? Ipca, valuations ?? [new ValuationInput(CaseInstance.First, 50_000.555m, 20_000m, null)]);

    private static Claim Create(ClaimInput? input = null) => Claim.Create(Guid.CreateVersion7(), CaseId, input ?? Input(), Author, Created).Value;

    [Fact]
    public void A_new_claim_records_its_values_in_the_history()
    {
        var claim = Create();

        claim.Description.ShouldBe("Horas extras de 2023");
        claim.CorrectionIndex.ShouldBe(ClaimCorrectionIndex.IpcaE);
        claim.InterestFrom.ShouldBe(new DateOnly(2024, 2, 5));
        claim.Valuations.Single().Requested.ShouldBe(50_000.56m);
        claim.Changes.Select(c => (c.Kind, c.OldAmount, c.NewAmount, c.ChangedBy, c.ChangedAt))
            .ShouldBe([(ClaimValueKind.Requested, null, 50_000.56m, Author, Created), (ClaimValueKind.Estimated, null, 20_000m, Author, Created)]);
        claim.Changes.ShouldAllBe(c => c.LegalCaseId == CaseId && c.ClaimId == claim.Id);
    }

    [Fact]
    public void Editing_records_only_the_values_that_changed_including_a_removed_instance()
    {
        var claim = Create(Input([new ValuationInput(CaseInstance.First, 50_000m, 20_000m, null), new ValuationInput(CaseInstance.Second, 10m, null, null)]));
        var firstId = claim.Valuations.Single(v => v.Instance == CaseInstance.First).Id;

        claim.Update(Input([new ValuationInput(CaseInstance.First, 50_000m, 25_000m, 30_000m)]), Editor, Edited).IsSuccess.ShouldBeTrue();

        claim.Valuations.Single().Id.ShouldBe(firstId);
        claim.Changes.Where(c => c.ChangedAt == Edited).Select(c => (c.Instance, c.Kind, c.OldAmount, c.NewAmount))
            .ShouldBe(
                [
                    (CaseInstance.Second, ClaimValueKind.Requested, 10m, null),
                    (CaseInstance.First, ClaimValueKind.Estimated, 20_000m, 25_000m),
                    (CaseInstance.First, ClaimValueKind.Awarded, null, 30_000m),
                ],
                ignoreOrder: true);
        claim.Changes.Where(c => c.ChangedAt == Edited).ShouldAllBe(c => c.ChangedBy == Editor);
    }

    [Fact]
    public void Without_interest_the_interest_fields_are_cleared()
    {
        var claim = Create(Input(correction: new CorrectionSettings(ClaimCorrectionIndex.Selic, new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1), 0m), description: "  "));

        claim.Description.ShouldBeNull();
        claim.InterestFrom.ShouldBeNull();
        claim.MonthlyInterestPercent.ShouldBeNull();

        claim.Update(new ClaimInput(Overtime, null, null, null), Editor, Edited).IsSuccess.ShouldBeTrue();
        claim.CorrectionIndex.ShouldBeNull();
        claim.CorrectFrom.ShouldBeNull();
        claim.Valuations.ShouldBeEmpty();
    }

    [Fact]
    public void Invalid_values_and_correction_are_refused()
    {
        Claim.Create(Guid.CreateVersion7(), CaseId, Input([new ValuationInput(CaseInstance.First, 1m, null, null), new ValuationInput(CaseInstance.First, 2m, null, null)]), Author, Created)
            .Error.ShouldBe(LitigationErrors.ValuationInstanceDuplicated);
        Claim.Create(Guid.CreateVersion7(), CaseId, Input([new ValuationInput(CaseInstance.First, null, -1m, null)]), Author, Created)
            .Error.ShouldBe(LitigationErrors.ValueInvalid);
        Claim.Create(Guid.CreateVersion7(), CaseId, Input(correction: Ipca with { Index = ClaimCorrectionIndex.Selic }), Author, Created)
            .Error.ShouldBe(LitigationErrors.ClaimCorrectionInvalid);
        Claim.Create(Guid.CreateVersion7(), CaseId, Input(correction: Ipca with { InterestFrom = null }), Author, Created)
            .Error.ShouldBe(LitigationErrors.ClaimCorrectionInvalid);
        Claim.Create(Guid.CreateVersion7(), CaseId, Input(correction: Ipca with { MonthlyInterestPercent = 101m }), Author, Created)
            .Error.ShouldBe(LitigationErrors.ClaimCorrectionInvalid);

        var claim = Create();
        claim.Update(Input([new ValuationInput(CaseInstance.First, null, null, -5m)]), Editor, Edited).Error.ShouldBe(LitigationErrors.ValueInvalid);
        claim.Changes.Count.ShouldBe(2);
    }

    [Fact]
    public void Each_valuation_tells_its_amounts_by_kind()
    {
        var valuation = Create(Input([new ValuationInput(CaseInstance.Superior, 1m, 2m, 3m)])).Valuations.Single();

        valuation.Amount(ClaimValueKind.Requested).ShouldBe(1m);
        valuation.Amount(ClaimValueKind.Estimated).ShouldBe(2m);
        valuation.Amount(ClaimValueKind.Awarded).ShouldBe(3m);
        valuation.LegalCaseId.ShouldBe(CaseId);
    }
}

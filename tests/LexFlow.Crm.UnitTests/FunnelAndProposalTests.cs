using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Opportunities;
using LexFlow.Crm.Domain.People;
using LexFlow.Crm.Domain.Proposals;

namespace LexFlow.Crm.UnitTests;

public sealed class FunnelAndProposalTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 18);

    private static OpportunityDetails Details(string name = "Construtora Horizonte", string? document = "11222333000181") =>
        new("Contencioso trabalhista", null, new ProspectInput(PersonKind.Company, name, document, "Contato@Horizonte.com.br", null), null, 50_000m, null, null, null);

    private static Opportunity Opportunity() => Domain.Opportunities.Opportunity.Create(Guid.CreateVersion7(), Details(), Now).Value;

    private static readonly ProposalItemInput Monthly = new("Acompanhamento mensal", FeeKind.Monthly, 12, 3_500m, null);
    private static readonly ProposalItemInput Success = new("Êxito sobre o valor recuperado", FeeKind.Success, 0, null, 20m);

    private static Proposal Draft(params ProposalItemInput[] items) =>
        Proposal.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), new ProposalDetails("Proposta 2026", "Defesa nas reclamações", null, Today.AddDays(30), null, items)).Value;

    [Fact]
    public void An_opportunity_starts_as_a_lead_and_moves_freely_between_open_stages()
    {
        var opportunity = Opportunity();

        opportunity.Stage.ShouldBe(OpportunityStage.Lead);
        opportunity.ProspectDocument.ShouldBe("11222333000181");
        opportunity.ProspectEmail.ShouldBe("contato@horizonte.com.br");
        opportunity.MoveTo(OpportunityStage.Proposal, Now).IsSuccess.ShouldBeTrue();
        opportunity.MoveTo(OpportunityStage.Meeting, Now.AddHours(1)).IsSuccess.ShouldBeTrue();
        opportunity.Stage.ShouldBe(OpportunityStage.Meeting);
        opportunity.StageChangedAt.ShouldBe(Now.AddHours(1));
        opportunity.MoveTo(OpportunityStage.Won, Now).Error.ShouldBe(CrmErrors.OpportunityStageInvalid);
        opportunity.MoveTo(OpportunityStage.Lost, Now).Error.ShouldBe(CrmErrors.OpportunityStageInvalid);
    }

    [Fact]
    public void A_lost_opportunity_keeps_the_reason_until_reopened_and_a_won_one_is_final()
    {
        var lost = Opportunity();
        var reason = Guid.CreateVersion7();

        lost.Lose(reason, " Preço acima do orçamento ", Now).IsSuccess.ShouldBeTrue();
        lost.Lose(reason, null, Now).Error.ShouldBe(CrmErrors.OpportunityLost);
        lost.LossReasonId.ShouldBe(reason);
        lost.LossNote.ShouldBe("Preço acima do orçamento");
        lost.MoveTo(OpportunityStage.Lead, Now).IsSuccess.ShouldBeTrue();
        lost.LossReasonId.ShouldBeNull();

        var won = Opportunity();
        var client = Guid.CreateVersion7();
        won.Win(client, Now).IsSuccess.ShouldBeTrue();
        won.PersonId.ShouldBe(client);
        won.MoveTo(OpportunityStage.Lead, Now).Error.ShouldBe(CrmErrors.OpportunityWon);
        won.Lose(reason, null, Now).Error.ShouldBe(CrmErrors.OpportunityWon);
        won.Update(Details()).Error.ShouldBe(CrmErrors.OpportunityWon);
    }

    [Fact]
    public void Changing_the_prospect_name_or_document_unlinks_the_conflict_check()
    {
        var opportunity = Opportunity();
        opportunity.LinkConflictCheck(Guid.CreateVersion7()).IsSuccess.ShouldBeTrue();

        opportunity.Update(Details() with { Title = "Outro título", EstimatedValue = 10m }).IsSuccess.ShouldBeTrue();
        opportunity.ConflictCheckId.ShouldNotBeNull("só o título e o valor mudaram");

        opportunity.Update(Details("Construtora Horizonte S.A.")).IsSuccess.ShouldBeTrue();
        opportunity.ConflictCheckId.ShouldBeNull();
    }

    [Fact]
    public void An_invalid_prospect_document_or_negative_value_is_refused()
    {
        Domain.Opportunities.Opportunity.Create(Guid.CreateVersion7(), Details(document: "11222333000100"), Now).Error.ShouldBe(CrmErrors.InvalidCnpj);
        Domain.Opportunities.Opportunity.Create(Guid.CreateVersion7(), Details() with { EstimatedValue = -1m }, Now).Error.ShouldBe(CrmErrors.OpportunityValueInvalid);
    }

    [Fact]
    public void Sending_a_proposal_moves_an_early_opportunity_to_the_proposal_stage()
    {
        var opportunity = Opportunity();
        opportunity.ProposalSent(Now);
        opportunity.Stage.ShouldBe(OpportunityStage.Proposal);
    }

    [Fact]
    public void The_total_sums_the_items_with_an_amount_and_leaves_success_fees_out()
    {
        var proposal = Draft(Monthly, Success, new ProposalItemInput("Audiências", FeeKind.PerAct, 3, 800.555m, null));

        proposal.Total.ShouldBe((12 * 3_500m) + (3 * 800.56m));
        proposal.Items.Single(i => i.Kind == FeeKind.Success).Amount.ShouldBeNull();
        proposal.Items.Single(i => i.Kind == FeeKind.Success).Quantity.ShouldBe(1);
        proposal.Items.Select(i => i.Position).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Items_need_an_amount_or_a_valid_percentage()
    {
        Proposal.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), new ProposalDetails("P", null, null, null, null, [new ProposalItemInput("x", FeeKind.Fixed, 1, null, null)]))
            .Error.ShouldBe(CrmErrors.ProposalAmountInvalid);
        Proposal.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), new ProposalDetails("P", null, null, null, null, [new ProposalItemInput("x", FeeKind.Success, 1, null, 120m)]))
            .Error.ShouldBe(CrmErrors.ProposalPercentageInvalid);
    }

    [Fact]
    public void The_author_cannot_approve_and_only_an_approved_proposal_is_sent()
    {
        var proposal = Draft(Monthly);
        var author = proposal.CreatedBy;
        var submitter = Guid.CreateVersion7();
        var partner = Guid.CreateVersion7();

        proposal.Send(Guid.CreateVersion7(), Today, Now).Error.ShouldBe(CrmErrors.ProposalNotApproved);
        proposal.Submit(submitter, Now).IsSuccess.ShouldBeTrue();
        proposal.Update(new ProposalDetails("Mudou", null, null, null, null, [Monthly])).Error.ShouldBe(CrmErrors.ProposalNotDraft);
        proposal.Approve(submitter, Now, soleMember: false).Error.ShouldBe(CrmErrors.ProposalSelfApproval);
        proposal.Approve(author, Now, soleMember: false).Error.ShouldBe(CrmErrors.ProposalSelfApproval);
        proposal.Approve(partner, Now, soleMember: false).IsSuccess.ShouldBeTrue();
        proposal.ApprovedBy.ShouldBe(partner);

        var document = Guid.CreateVersion7();
        proposal.Send(document, Today, Now).IsSuccess.ShouldBeTrue();
        proposal.DocumentId.ShouldBe(document);
        proposal.CanDelete().Error.ShouldBe(CrmErrors.ProposalCannotDelete);
        proposal.Accept(Today, Now).IsSuccess.ShouldBeTrue();
        proposal.Decline(null, Now).Error.ShouldBe(CrmErrors.ProposalNotSent);
    }

    [Fact]
    public void The_only_member_of_the_office_approves_the_own_proposal()
    {
        var proposal = Draft(Monthly);
        var author = Guid.CreateVersion7();
        proposal.Submit(author, Now);

        proposal.Approve(author, Now, soleMember: true).IsSuccess.ShouldBeTrue();
        proposal.ApprovedBy.ShouldBe(author);
        proposal.Status.ShouldBe(ProposalStatus.Approved);
    }

    [Fact]
    public void An_empty_draft_cannot_be_submitted_and_returning_clears_the_approval()
    {
        Draft().Submit(Guid.CreateVersion7(), Now).Error.ShouldBe(CrmErrors.ProposalItemsRequired);

        var proposal = Draft(Monthly);
        proposal.ReturnToDraft(null).Error.ShouldBe(CrmErrors.ProposalCannotReturn);
        proposal.Submit(Guid.CreateVersion7(), Now);
        proposal.Approve(Guid.CreateVersion7(), Now, soleMember: false);
        proposal.ReturnToDraft(" Rever o valor mensal ").IsSuccess.ShouldBeTrue();

        proposal.Status.ShouldBe(ProposalStatus.Draft);
        proposal.ApprovedBy.ShouldBeNull();
        proposal.SubmittedBy.ShouldBeNull();
        proposal.ReturnNote.ShouldBe("Rever o valor mensal");
    }

    [Fact]
    public void An_expired_proposal_is_neither_sent_nor_accepted()
    {
        var proposal = Draft(Monthly);
        proposal.Submit(Guid.CreateVersion7(), Now);
        proposal.Approve(Guid.CreateVersion7(), Now, soleMember: false);

        proposal.Send(Guid.CreateVersion7(), Today.AddDays(31), Now).Error.ShouldBe(CrmErrors.ProposalExpired);
        proposal.Send(Guid.CreateVersion7(), Today, Now).IsSuccess.ShouldBeTrue();
        proposal.Accept(Today.AddDays(31), Now).Error.ShouldBe(CrmErrors.ProposalExpired);
    }

    [Fact]
    public void Superseding_closes_only_open_proposals()
    {
        var open = Draft(Monthly);
        var declined = Draft(Monthly);
        declined.Submit(Guid.CreateVersion7(), Now);
        declined.Approve(Guid.CreateVersion7(), Now, soleMember: false);
        declined.Send(Guid.CreateVersion7(), Today, Now);
        declined.Decline("Achou caro", Now);

        open.Supersede();
        declined.Supersede();

        open.Status.ShouldBe(ProposalStatus.Superseded);
        declined.Status.ShouldBe(ProposalStatus.Declined);
        declined.DeclineNote.ShouldBe("Achou caro");
    }
}

using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Crm.Application.Abstractions;
using LexFlow.Crm.Contracts;
using LexFlow.Crm.Domain;
using LexFlow.Crm.Domain.Conflicts;
using LexFlow.Crm.Domain.Opportunities;
using LexFlow.Crm.Domain.People;
using LexFlow.Crm.Domain.Proposals;
using LexFlow.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Crm.Application.Proposals;

/// <summary>A proposta aceita e o cliente que ela gerou (ou reaproveitou).</summary>
public sealed record AcceptedProposalResponse(ProposalResponse Proposal, Guid ClientPersonId);

/// <summary>
/// Aceite da proposta enviada (RF-08): exige a verificação de conflito do interessado já prosseguida (decisão de
/// 2026-10-17); o interessado vira cliente (a pessoa ligada, a do mesmo documento, ou um cadastro novo), a oportunidade
/// fecha ganha, as outras propostas abertas dela ficam substituídas e sai <see cref="ProposalAcceptedIntegrationEvent"/>
/// para o contrato de honorários (Etapa 10).
/// </summary>
public sealed record AcceptProposalCommand(Guid Id) : ICommand<AcceptedProposalResponse>;

internal sealed class AcceptProposalHandler(ICrmDbContext db, IIntegrationEventPublisher events, IIdGenerator ids, IClock clock)
    : ICommandHandler<AcceptProposalCommand, AcceptedProposalResponse>
{
    public async Task<Result<AcceptedProposalResponse>> Handle(AcceptProposalCommand command, CancellationToken cancellationToken)
    {
        var proposal = await db.Proposals.Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == command.Id, cancellationToken);
        if (proposal is null)
        {
            return CrmErrors.ProposalNotFound;
        }

        var opportunity = await db.Opportunities.SingleAsync(o => o.Id == proposal.OpportunityId, cancellationToken);
        if (opportunity.IsClosed)
        {
            return opportunity.Stage == OpportunityStage.Won ? CrmErrors.OpportunityWon : CrmErrors.OpportunityLost;
        }

        // Uma falha depois daqui não grava nada: o caso de uso só chama SaveChanges no fim.
        var accepted = proposal.Accept(clock.Today, clock.UtcNow);
        if (accepted.IsFailure)
        {
            return accepted.Error;
        }

        var cleared = opportunity.ConflictCheckId is { } checkId
            && await db.ConflictChecks.AnyAsync(c => c.Id == checkId && c.Side == ConflictSide.Client && c.Proceeded, cancellationToken);
        if (!cleared)
        {
            return CrmErrors.ConflictCheckRequired;
        }

        var client = await ClientOfAsync(opportunity, cancellationToken);
        if (client.IsFailure)
        {
            return client.Error;
        }

        opportunity.Win(client.Value.Id, clock.UtcNow);
        foreach (var other in await db.Proposals.Where(p => p.OpportunityId == opportunity.Id && p.Id != proposal.Id).ToListAsync(cancellationToken))
        {
            other.Supersede();
        }

        events.Publish(new ProposalAcceptedIntegrationEvent(
            proposal.Id,
            opportunity.Id,
            client.Value.Id,
            proposal.Currency,
            proposal.Total,
            [.. proposal.Items.OrderBy(i => i.Position).Select(i => new AcceptedProposalItem(i.Description, i.Kind.ToString(), i.Quantity, i.UnitAmount, i.Percentage))],
            proposal.AcceptedAt!.Value));
        await db.SaveChangesAsync(cancellationToken);
        return new AcceptedProposalResponse(proposal.ToResponse(), client.Value.Id);
    }

    /// <summary>A pessoa ligada à oportunidade, a do mesmo documento ou um cadastro novo, sempre com o papel Cliente.</summary>
    private async Task<Result<Person>> ClientOfAsync(Opportunity opportunity, CancellationToken cancellationToken)
    {
        var person = opportunity.PersonId is { } personId
            ? await db.People.Include(p => p.Roles).SingleOrDefaultAsync(p => p.Id == personId, cancellationToken)
            : null;
        if (person is null && opportunity.ProspectDocument is { } document)
        {
            person = await db.People.Include(p => p.Roles).SingleOrDefaultAsync(p => p.Document == document, cancellationToken);
        }

        if (person is not null)
        {
            var added = person.AddRole(PersonRoleKind.Client);
            return added.IsFailure ? added.Error : person;
        }

        var created = Person.Create(ids.NewId(), opportunity.ProspectKind, new PersonDetails(
            opportunity.ProspectName, null, opportunity.ProspectDocument, opportunity.ProspectEmail, opportunity.ProspectPhone, null, null, null, [PersonRoleKind.Client], [], []));
        if (created.IsFailure)
        {
            return created.Error;
        }

        db.People.Add(created.Value);
        return created.Value;
    }
}

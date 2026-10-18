using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Crm.Contracts;

/// <summary>Item da proposta aceita; <see cref="Kind"/> é o nome da modalidade (Fixed, Monthly, Hourly, Success, PerAct).</summary>
public sealed record AcceptedProposalItem(string Description, string Kind, decimal Quantity, decimal? UnitAmount, decimal? Percentage);

/// <summary>
/// Uma proposta de honorários foi aceita e o interessado virou cliente (RF-08). O Finance cria o contrato de
/// honorários com os itens (Etapa 10); o caso, opcional, entra com o módulo de processos (Etapa 5).
/// </summary>
public sealed record ProposalAcceptedIntegrationEvent(
    Guid ProposalId,
    Guid OpportunityId,
    Guid ClientPersonId,
    string Currency,
    decimal Total,
    IReadOnlyList<AcceptedProposalItem> Items,
    DateTimeOffset AcceptedAt) : IntegrationEvent;

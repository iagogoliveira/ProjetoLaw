using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Litigation.Contracts;

/// <summary>Uma parcela do acordo homologado.</summary>
public sealed record SettlementInstallmentLine(Guid InstallmentId, int Number, DateOnly DueOn, decimal Amount);

/// <summary>
/// Acordo homologado em juízo (RF-16). O financeiro (Etapa 10) consome para gerar as contas: a receber quando
/// <see cref="Direction"/> é <c>ClientReceives</c>, a pagar quando é <c>ClientPays</c>.
/// </summary>
public sealed record SettlementHomologatedIntegrationEvent(
    Guid SettlementId,
    Guid CaseId,
    string Direction,
    string Currency,
    decimal AgreedAmount,
    IReadOnlyList<SettlementInstallmentLine> Installments,
    DateOnly HomologatedOn) : IntegrationEvent;

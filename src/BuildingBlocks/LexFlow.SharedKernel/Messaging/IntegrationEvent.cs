namespace LexFlow.SharedKernel.Messaging;

/// <summary>
/// Evento de integração entre módulos/processos (seção 4.5), definido no projeto <c>Contracts</c> do módulo emissor.
/// Os metadados são preenchidos pelo publicador; o consumo é idempotente por <see cref="EventId"/>.
/// Versione criando um novo tipo (ex.: <c>CaseOpenedV2</c>) em vez de alterar um contrato publicado.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; }

    public Guid OrganizationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string? CorrelationId { get; init; }
}

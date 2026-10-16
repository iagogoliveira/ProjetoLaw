using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Application.Abstractions.Messaging;

/// <summary>
/// Publica eventos de integração pelo outbox: o evento é gravado no mesmo commit do próximo <c>SaveChanges</c>
/// do módulo e só é enviado depois do commit. Em rollback, nada é publicado.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>Preenche <c>EventId</c>, <c>OrganizationId</c>, <c>OccurredAt</c> e <c>CorrelationId</c> quando vazios.</summary>
    void Publish<TEvent>(TEvent integrationEvent)
        where TEvent : IntegrationEvent;
}

/// <summary>
/// Consumidor de evento de integração (no projeto Infrastructure do módulo consumidor). Deve ser <c>public sealed</c>
/// (exigência da geração de código do Wolverine) e normalmente herda de <c>IntegrationEventConsumer</c>, que garante
/// processamento único por <c>EventId</c> e executa com o tenant do evento.
/// </summary>
#pragma warning disable CA1711 // mesmo padrão de nomes dos demais handlers
public interface IIntegrationEventHandler<in TEvent>
#pragma warning restore CA1711
    where TEvent : IntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}

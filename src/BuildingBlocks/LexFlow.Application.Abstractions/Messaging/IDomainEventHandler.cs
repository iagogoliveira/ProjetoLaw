using LexFlow.SharedKernel.Domain;

namespace LexFlow.Application.Abstractions.Messaging;

/// <summary>
/// Handler de evento de domínio: roda dentro do mesmo <c>SaveChanges</c> (mesma transação) que gerou o evento.
/// Para reagir em outro módulo, publique um evento de integração (outbox).
/// </summary>
#pragma warning disable CA1711 // "EventHandler" aqui é o handler do pipeline, não um delegate .NET; mantém o padrão ICommandHandler/IQueryHandler
public interface IDomainEventHandler<in TEvent>
#pragma warning restore CA1711
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

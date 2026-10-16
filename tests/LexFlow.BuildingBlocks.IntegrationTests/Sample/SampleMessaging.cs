using System.Collections.Concurrent;
using LexFlow.Application.Abstractions;
using LexFlow.Infrastructure.Common.Messaging;
using LexFlow.Sample.Contracts;
using LexFlow.Sample.Domain;
using LexFlow.SharedKernel.Messaging;

namespace LexFlow.Sample.Contracts
{
    /// <summary>Evento de integração de exemplo (no projeto Contracts do módulo emissor).</summary>
    public sealed record MatterRegisteredIntegrationEvent(Guid MatterId, string Title) : IntegrationEvent;
}

namespace LexFlow.Sample.Infrastructure
{
    /// <summary>Consumidor de exemplo: registra uma linha na linha do tempo do processo.</summary>
    public sealed class MatterRegisteredConsumer(SampleDbContext db, IClock clock)
        : IntegrationEventConsumer<MatterRegisteredIntegrationEvent, SampleDbContext>(db, clock)
    {
        public const string TimelineText = "Evento de integração recebido";

        /// <summary>Reentregas descartadas, por EventId (observação para os testes).</summary>
        public static ConcurrentDictionary<Guid, int> SkippedDuplicates { get; } = new();

        protected override Task ConsumeAsync(MatterRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Db.Timeline.Add(new MatterTimelineEntry(Guid.CreateVersion7(), integrationEvent.MatterId, TimelineText));
            return Task.CompletedTask;
        }

        protected override void OnDuplicateSkipped(MatterRegisteredIntegrationEvent integrationEvent) =>
            SkippedDuplicates.AddOrUpdate(integrationEvent.EventId, 1, (_, count) => count + 1);
    }
}

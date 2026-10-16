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

    /// <summary>Evento de plataforma de exemplo (sem organização), como os de contas de usuário.</summary>
    public sealed record AccountNoticeIntegrationEvent(Guid UserId) : PlatformIntegrationEvent;
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

    /// <summary>Consumidor de evento de plataforma: anota o tenant visto durante o consumo (deve ser nenhum).</summary>
    public sealed class AccountNoticeConsumer(SampleDbContext db, IClock clock, ITenantContext tenant)
        : IntegrationEventConsumer<AccountNoticeIntegrationEvent, SampleDbContext>(db, clock)
    {
        /// <summary>Consumos por UserId, com o tenant ativo em cada um.</summary>
        public static ConcurrentDictionary<Guid, List<Guid?>> Consumed { get; } = new();

        protected override Task ConsumeAsync(AccountNoticeIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Consumed.AddOrUpdate(integrationEvent.UserId, _ => [tenant.OrganizationId], (_, seen) => [.. seen, tenant.OrganizationId]);
            return Task.CompletedTask;
        }
    }
}

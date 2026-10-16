using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Infrastructure.Common.Persistence;
using LexFlow.SharedKernel.Domain;
using LexFlow.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Common.Messaging;

/// <summary>
/// Registro de evento já processado por um consumidor, gravado no schema do módulo consumidor no mesmo
/// <c>SaveChanges</c> do efeito. A chave (<c>event_id</c>, <c>consumer</c>) garante efeito único mesmo com reentrega.
/// </summary>
[NotAudited]
public sealed class ProcessedIntegrationEvent
{
    public Guid EventId { get; init; }

    public required string Consumer { get; init; }

    public DateTimeOffset ProcessedAt { get; init; }
}

internal sealed class ProcessedIntegrationEventConfiguration : IEntityTypeConfiguration<ProcessedIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedIntegrationEvent> builder)
    {
        builder.ToTable("processed_integration_events");
        builder.HasKey(e => new { e.EventId, e.Consumer });
        builder.Property(e => e.Consumer).HasMaxLength(200);
    }
}

/// <summary>
/// Base dos consumidores de eventos de integração: executa com o tenant e a correlação do evento, ignora eventos já
/// processados por este consumidor e grava o efeito e o registro de processamento no mesmo commit.
/// </summary>
public abstract class IntegrationEventConsumer<TEvent, TDbContext>(TDbContext db, IClock clock) : IIntegrationEventHandler<TEvent>
    where TEvent : IntegrationEvent
    where TDbContext : ModuleDbContextBase
{
    protected TDbContext Db { get; } = db;

    /// <summary>Nome estável do consumidor (padrão: nome do tipo). Não renomeie consumidores em produção sem migrar.</summary>
    protected virtual string ConsumerName => GetType().Name;

    public async Task Handle(TEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        using var scope = AmbientContext.Begin(integrationEvent.OrganizationId, integrationEvent.CorrelationId);

        var alreadyProcessed = await Db.Set<ProcessedIntegrationEvent>()
            .AnyAsync(p => p.EventId == integrationEvent.EventId && p.Consumer == ConsumerName, cancellationToken);
        if (alreadyProcessed)
        {
            OnDuplicateSkipped(integrationEvent);
            return;
        }

        await ConsumeAsync(integrationEvent, cancellationToken);
        Db.Set<ProcessedIntegrationEvent>().Add(new ProcessedIntegrationEvent
        {
            EventId = integrationEvent.EventId,
            Consumer = ConsumerName,
            ProcessedAt = clock.UtcNow,
        });
        await Db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Chamado quando uma reentrega de evento já processado é descartada (ponto para métricas).</summary>
    protected virtual void OnDuplicateSkipped(TEvent integrationEvent)
    {
    }

    /// <summary>Aplica o efeito do evento alterando <see cref="Db"/>; não chame <c>SaveChanges</c>.</summary>
    protected abstract Task ConsumeAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

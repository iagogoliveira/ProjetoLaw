using System.Data.Common;
using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.SharedKernel.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wolverine.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Common.Messaging;

/// <summary>Eventos publicados no escopo atual, aguardando o próximo <c>SaveChanges</c> de um módulo.</summary>
internal sealed class PendingIntegrationEvents
{
    private readonly List<IntegrationEvent> _events = [];

    public bool HasEvents => _events.Count > 0;

    public void Add(IntegrationEvent integrationEvent) => _events.Add(integrationEvent);

    public IReadOnlyList<IntegrationEvent> TakeAll()
    {
        var events = _events.ToList();
        _events.Clear();
        return events;
    }
}

internal sealed class IntegrationEventPublisher(
    PendingIntegrationEvents pending,
    IClock clock,
    IIdGenerator ids,
    ITenantContext tenant,
    IRequestContext request) : IIntegrationEventPublisher
{
    public void Publish<TEvent>(TEvent integrationEvent)
        where TEvent : IntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var organization = integrationEvent switch
        {
            PlatformIntegrationEvent when integrationEvent.OrganizationId != Guid.Empty =>
                throw new InvalidOperationException($"{typeof(TEvent).Name} é da plataforma e não leva organização."),
            PlatformIntegrationEvent => Guid.Empty,
            _ when integrationEvent.OrganizationId != Guid.Empty => integrationEvent.OrganizationId,
            _ => tenant.OrganizationId ?? throw new InvalidOperationException($"{typeof(TEvent).Name} publicado sem organização."),
        };

        pending.Add(integrationEvent with
        {
            EventId = integrationEvent.EventId == Guid.Empty ? ids.NewId() : integrationEvent.EventId,
            OrganizationId = organization,
            OccurredAt = integrationEvent.OccurredAt == default ? clock.UtcNow : integrationEvent.OccurredAt,
            CorrelationId = integrationEvent.CorrelationId ?? request.CorrelationId,
        });
    }
}

/// <summary>
/// Liga o <see cref="IIntegrationEventPublisher"/> ao outbox do Wolverine: no <c>SaveChanges</c>, inscreve o DbContext
/// no outbox e grava os envelopes na mesma transação; libera o envio só depois do commit (no fim do <c>SaveChanges</c>
/// ou, havendo transação explícita, no commit dela). Em rollback os envelopes somem junto com os dados.
/// </summary>
internal sealed class OutboxInterceptor(PendingIntegrationEvents pending, IDbContextOutbox outbox)
    : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private bool _flushOnCommit;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context && pending.HasEvents)
        {
            outbox.Enroll(context);
            foreach (var integrationEvent in pending.TakeAll())
            {
                await outbox.PublishAsync(integrationEvent);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (ReferenceEquals(outbox.ActiveContext, eventData.Context))
        {
            if (eventData.Context!.Database.CurrentTransaction is null)
            {
                await outbox.FlushOutgoingMessagesAsync();
            }
            else
            {
                _flushOnCommit = true;
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_flushOnCommit)
        {
            _flushOnCommit = false;
            await outbox.FlushOutgoingMessagesAsync();
        }
    }

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        _flushOnCommit = false;
        return Task.CompletedTask;
    }
}

using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Sample.Contracts;
using LexFlow.Sample.Domain;
using LexFlow.Sample.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.IntegrationTests;

/// <summary>
/// Critério de aceite da Etapa 1: um evento de integração gravado no outbox é publicado e consumido uma única vez,
/// mesmo com reentrega. Usa Wolverine, PostgreSQL e RabbitMQ reais.
/// </summary>
public sealed class OutboxTests(MessagingFixture fixture) : IClassFixture<MessagingFixture>
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(3);

    private const string CountEffects =
        "SELECT count(*) FROM sample.timeline WHERE matter_id = $1 AND text = 'Evento de integração recebido'";

    private readonly Guid _organization = Guid.CreateVersion7();

    /// <summary>Grava o processo e publica o evento no mesmo SaveChanges (opcionalmente dentro de uma transação explícita).</summary>
    private async Task SaveAndPublishAsync(Guid matterId, Guid eventId, bool addMatter = true, bool rollback = false)
    {
        using var ambient = AmbientContext.Begin(_organization, "outbox-test");
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredKeyedService<IUnitOfWork>("Sample");

        await using var transaction = await unitOfWork.BeginTransactionAsync(Ct);
        if (addMatter)
        {
            db.Matters.Add(new Matter(matterId, "Ação de cobrança", null));
        }

        publisher.Publish(new MatterRegisteredIntegrationEvent(matterId, "Ação de cobrança") { EventId = eventId });
        await db.SaveChangesAsync(Ct);

        if (rollback)
        {
            await transaction.RollbackAsync(Ct);
        }
        else
        {
            await transaction.CommitAsync(Ct);
        }
    }

    private async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (!await condition())
        {
            if (elapsed.Elapsed > Timeout)
            {
                throw new TimeoutException("Condição não atendida dentro do tempo limite.");
            }

            await Task.Delay(200, Ct);
        }
    }

    [Fact]
    public async Task Event_saved_in_outbox_is_published_and_consumed_exactly_once()
    {
        var matterId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();

        await SaveAndPublishAsync(matterId, eventId);

        await WaitUntilAsync(async () => await fixture.CountAsync(CountEffects, matterId) == 1);
        await Task.Delay(QuietPeriod, Ct);
        (await fixture.CountAsync(CountEffects, matterId)).ShouldBe(1);
        (await fixture.CountAsync("SELECT count(*) FROM sample.processed_integration_events WHERE event_id = $1", eventId)).ShouldBe(1);
    }

    [Fact]
    public async Task Redelivered_event_is_not_processed_twice()
    {
        var matterId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        await SaveAndPublishAsync(matterId, eventId);
        await WaitUntilAsync(async () => await fixture.CountAsync(CountEffects, matterId) == 1);

        // Reentrega: o mesmo evento (mesmo EventId) chega de novo por outra mensagem.
        await SaveAndPublishAsync(matterId, eventId, addMatter: false);

        await WaitUntilAsync(() => Task.FromResult(MatterRegisteredConsumer.SkippedDuplicates.ContainsKey(eventId)));
        (await fixture.CountAsync(CountEffects, matterId)).ShouldBe(1);
        (await fixture.CountAsync("SELECT count(*) FROM sample.processed_integration_events WHERE event_id = $1", eventId)).ShouldBe(1);
    }

    [Fact]
    public async Task Rolled_back_transaction_publishes_nothing()
    {
        var matterId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();

        await SaveAndPublishAsync(matterId, eventId, rollback: true);

        await Task.Delay(QuietPeriod, Ct);
        (await fixture.CountAsync(CountEffects, matterId)).ShouldBe(0);
        (await fixture.CountAsync("SELECT count(*) FROM sample.matters WHERE id = $1", matterId)).ShouldBe(0);
        (await fixture.CountAsync("SELECT count(*) FROM sample.processed_integration_events WHERE event_id = $1", eventId)).ShouldBe(0);
    }

    [Fact]
    public async Task Consumer_runs_with_the_tenant_of_the_event()
    {
        var matterId = Guid.CreateVersion7();

        await SaveAndPublishAsync(matterId, Guid.CreateVersion7());

        await WaitUntilAsync(async () => await fixture.CountAsync(CountEffects, matterId) == 1);
        (await fixture.CountAsync(
            "SELECT count(*) FROM sample.timeline WHERE matter_id = $1 AND organization_id = $2",
            matterId,
            _organization)).ShouldBe(2); // entrada do evento de domínio + entrada do consumidor, ambas na organização do evento
    }

    [Fact]
    public async Task Platform_event_is_published_without_tenant_and_consumed_without_tenant()
    {
        var userId = Guid.CreateVersion7();
        await using (var scope = fixture.Host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>().Publish(new AccountNoticeIntegrationEvent(userId));
            await scope.ServiceProvider.GetRequiredService<SampleDbContext>().SaveChangesAsync(Ct);
        }

        await WaitUntilAsync(() => Task.FromResult(AccountNoticeConsumer.Consumed.ContainsKey(userId)));
        await Task.Delay(QuietPeriod, Ct);
        AccountNoticeConsumer.Consumed[userId].ShouldBe([null]);
    }

    [Fact]
    public async Task Platform_event_with_organization_is_rejected()
    {
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

        Should.Throw<InvalidOperationException>(() => publisher.Publish(new AccountNoticeIntegrationEvent(Guid.CreateVersion7()) { OrganizationId = _organization }));
    }
}

using LexFlow.Application.Abstractions.Messaging;
using LexFlow.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Infrastructure.Common.Persistence.Interceptors;

/// <summary>
/// Despacha os eventos de domínio dos agregados antes de gravar, para que as alterações feitas pelos handlers
/// entrem no mesmo <c>SaveChanges</c> (mesma transação). Repete enquanto handlers gerarem novos eventos.
/// </summary>
internal sealed class DomainEventsInterceptor(IServiceProvider services) : SaveChangesInterceptor
{
    private const int MaxRounds = 10;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is { } context)
        {
            await DispatchAsync(context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new NotSupportedException("Use SaveChangesAsync: eventos de domínio são despachados de forma assíncrona.");

    private async Task DispatchAsync(DbContext context, CancellationToken cancellationToken)
    {
        for (var round = 0; round < MaxRounds; round++)
        {
            var events = context.ChangeTracker.Entries<IHasDomainEvents>()
                .SelectMany(entry =>
                {
                    var pending = entry.Entity.DomainEvents.ToList();
                    entry.Entity.ClearDomainEvents();
                    return pending;
                })
                .ToList();

            if (events.Count == 0)
            {
                return;
            }

            foreach (var domainEvent in events)
            {
                var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
                foreach (var handler in services.GetServices(handlerType))
                {
                    var handle = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.Handle))!;
                    await (Task)handle.Invoke(handler, [domainEvent, cancellationToken])!;
                }
            }
        }

        throw new InvalidOperationException($"Eventos de domínio continuaram sendo gerados após {MaxRounds} rodadas (ciclo entre handlers?).");
    }
}

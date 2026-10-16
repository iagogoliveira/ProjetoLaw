using System.Reflection;
using FluentValidation;
using LexFlow.Application.Abstractions.Behaviors;
using LexFlow.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexFlow.Application.Abstractions;

public static class DependencyInjection
{
    /// <summary>
    /// Registra relógio, gerador de IDs, todos os handlers e validadores dos assemblies informados (Scrutor) e
    /// o pipeline de decorators. Deve ser chamado uma única vez, com os assemblies Application de todos os módulos.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock, TimeProviderClock>();
        services.TryAddSingleton<IIdGenerator, GuidV7IdGenerator>();

        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(c => c.AssignableToAny(typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IValidator<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Scrutor envolve na ordem de registro: o último decorator é o mais externo.
        // Resultado: Logging → Validação → (Autorização de recurso, Etapa 3) → Transação → Handler.
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(TransactionCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationCommandDecorator<,>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingCommandDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationQueryDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryDecorator<,>));

        return services;
    }
}

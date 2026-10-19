using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Application.Abstractions.Behaviors;

/// <summary>
/// Último decorator antes do handler: abre a transação do módulo do comando, confirma em sucesso e
/// desfaz em falha ou exceção. Sem <see cref="IUnitOfWork"/> registrado para o módulo, ou com transação
/// já aberta (comando chamado por outro), apenas repassa.
/// Comandos optantes em <see cref="ILocalTransactionCommand"/> usam o coordenador local (ADR 0035).
/// </summary>
internal sealed class TransactionCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IServiceProvider services)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginAsync(command, cancellationToken);
        if (transaction is null)
        {
            return await inner.Handle(command, cancellationToken);
        }

        var result = await inner.Handle(command, cancellationToken);
        if (result.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        return result;
    }

    private Task<IUnitOfWorkTransaction?> BeginAsync(TCommand command, CancellationToken cancellationToken)
    {
        var module = ModuleName.Of(typeof(TCommand));
        if (command is ILocalTransactionCommand local)
        {
            if (module is null || local.TransactionModules.Count == 0
                || !string.Equals(local.TransactionModules[0], module, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A transação local deve começar pelo módulo do comando.");
            }

            return BeginLocalAsync(local, cancellationToken);
        }

        var unitOfWork = module is null ? null : services.GetKeyedService<IUnitOfWork>(module);
        return unitOfWork is null || unitOfWork.HasActiveTransaction
            ? Task.FromResult<IUnitOfWorkTransaction?>(null)
            : BeginModuleAsync(unitOfWork, cancellationToken);
    }

    private async Task<IUnitOfWorkTransaction?> BeginLocalAsync(ILocalTransactionCommand command, CancellationToken cancellationToken) =>
        await services.GetRequiredService<ILocalTransactionCoordinator>().BeginAsync(command.TransactionModules, cancellationToken);

    private static async Task<IUnitOfWorkTransaction?> BeginModuleAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken) =>
        await unitOfWork.BeginTransactionAsync(cancellationToken);
}

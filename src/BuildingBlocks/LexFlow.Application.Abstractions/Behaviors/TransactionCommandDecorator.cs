using LexFlow.Application.Abstractions.Data;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.SharedKernel.Results;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Application.Abstractions.Behaviors;

/// <summary>
/// Último decorator antes do handler: abre a transação do módulo do comando, confirma em sucesso e
/// desfaz em falha ou exceção. Sem <see cref="IUnitOfWork"/> registrado para o módulo, ou com transação
/// já aberta (comando chamado por outro), apenas repassa.
/// </summary>
internal sealed class TransactionCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IServiceProvider services)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var module = ModuleName.Of(typeof(TCommand));
        var unitOfWork = module is null ? null : services.GetKeyedService<IUnitOfWork>(module);
        if (unitOfWork is null || unitOfWork.HasActiveTransaction)
        {
            return await inner.Handle(command, cancellationToken);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
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
}

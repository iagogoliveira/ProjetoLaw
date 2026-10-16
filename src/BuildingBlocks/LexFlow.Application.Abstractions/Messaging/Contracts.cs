using LexFlow.SharedKernel.Results;

namespace LexFlow.Application.Abstractions.Messaging;

/// <summary>Retorno vazio de comandos que não produzem valor.</summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}

/// <summary>Comando: altera estado. Passa por Logging → Validação → Autorização de recurso → Transação.</summary>
#pragma warning disable CA1040 // interfaces marcadoras: o tipo de resposta guia o pipeline
public interface ICommand<TResponse>;

/// <summary>Comando sem valor de retorno.</summary>
public interface ICommand : ICommand<Unit>;

/// <summary>Consulta: só leitura. Passa por Logging → Validação (sem transação).</summary>
public interface IQuery<TResponse>;
#pragma warning restore CA1040

/// <summary>Handler de comando. Implementações são <c>internal sealed</c> no projeto Application do módulo.</summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handler de consulta. Implementações são <c>internal sealed</c> no projeto Application do módulo.</summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}

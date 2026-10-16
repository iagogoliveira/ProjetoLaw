using System.Diagnostics;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace LexFlow.Application.Abstractions.Behaviors;

/// <summary>
/// Primeiro decorator do pipeline: registra nome, duração e código do erro. Nunca registra o conteúdo
/// do comando (pode ter CPF, textos de peças etc. — seção 13.4).
/// </summary>
internal static partial class PipelineLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "{RequestName} concluído em {ElapsedMs} ms")]
    public static partial void Succeeded(ILogger logger, string requestName, long elapsedMs);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "{RequestName} falhou com {ErrorCode} ({ErrorType}) em {ElapsedMs} ms")]
    public static partial void Failed(ILogger logger, string requestName, string errorCode, ErrorType errorType, long elapsedMs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "{RequestName} lançou exceção após {ElapsedMs} ms")]
    public static partial void Threw(ILogger logger, Exception exception, string requestName, long elapsedMs);

    public static async Task<Result<T>> Run<T>(ILogger logger, string requestName, Func<Task<Result<T>>> next)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await next();
            var elapsed = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (result.IsSuccess)
            {
                Succeeded(logger, requestName, elapsed);
            }
            else
            {
                Failed(logger, requestName, result.Error.Code, result.Error.Type, elapsed);
            }

            return result;
        }
        catch (Exception ex)
        {
            Threw(logger, ex, requestName, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}

internal sealed class LoggingCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandDecorator<TCommand, TResponse>> logger)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken) =>
        PipelineLog.Run(logger, typeof(TCommand).Name, () => inner.Handle(command, cancellationToken));
}

internal sealed class LoggingQueryDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    ILogger<LoggingQueryDecorator<TQuery, TResponse>> logger)
    : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken) =>
        PipelineLog.Run(logger, typeof(TQuery).Name, () => inner.Handle(query, cancellationToken));
}

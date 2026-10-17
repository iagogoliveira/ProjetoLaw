using LexFlow.Application.Abstractions.Messaging;
using LexFlow.Application.Abstractions.Security;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Application.Abstractions.Behaviors;

/// <summary>Executa os autorizadores de recurso do request; o primeiro que negar encerra o pipeline.</summary>
internal static class ResourceAuthorization
{
    public static async Task<Error?> AuthorizeAsync<T>(IEnumerable<IResourceAuthorizer<T>> authorizers, T request, CancellationToken cancellationToken)
    {
        foreach (var authorizer in authorizers)
        {
            var result = await authorizer.AuthorizeAsync(request, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        return null;
    }
}

internal sealed class ResourceAuthorizationCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IResourceAuthorizer<TCommand>> authorizers)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var error = await ResourceAuthorization.AuthorizeAsync(authorizers, command, cancellationToken);
        return error is null ? await inner.Handle(command, cancellationToken) : Result.Failure<TResponse>(error);
    }
}

internal sealed class ResourceAuthorizationQueryDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    IEnumerable<IResourceAuthorizer<TQuery>> authorizers)
    : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        var error = await ResourceAuthorization.AuthorizeAsync(authorizers, query, cancellationToken);
        return error is null ? await inner.Handle(query, cancellationToken) : Result.Failure<TResponse>(error);
    }
}

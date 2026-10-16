using FluentValidation;
using LexFlow.Application.Abstractions.Messaging;
using LexFlow.SharedKernel.Results;

namespace LexFlow.Application.Abstractions.Behaviors;

/// <summary>Executa os validadores FluentValidation do request e converte falhas em <see cref="ValidationError"/>.</summary>
internal static class RequestValidation
{
    public static async Task<ValidationError?> ValidateAsync<T>(IEnumerable<IValidator<T>> validators, T request, CancellationToken cancellationToken)
    {
        var failures = new List<FieldError>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors.Select(e => new FieldError(ToCamelCase(e.PropertyName), e.ErrorCode, e.ErrorMessage)));
        }

        return failures.Count == 0 ? null : new ValidationError(failures);
    }

    /// <summary>"ClientId" → "clientId", "Parties[0].Name" → "parties[0].name" (nomes do JSON da API).</summary>
    private static string ToCamelCase(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
}

internal sealed class ValidationCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        var error = await RequestValidation.ValidateAsync(validators, command, cancellationToken);
        return error is null ? await inner.Handle(command, cancellationToken) : error;
    }
}

internal sealed class ValidationQueryDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    IEnumerable<IValidator<TQuery>> validators)
    : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        var error = await RequestValidation.ValidateAsync(validators, query, cancellationToken);
        return error is null ? await inner.Handle(query, cancellationToken) : error;
    }
}

using System.Diagnostics.CodeAnalysis;

namespace LexFlow.SharedKernel.Results;

/// <summary>Resultado de uma operação sem valor de retorno. Exceções ficam para falhas inesperadas.</summary>
public class Result
{
    protected Result(Error? error)
    {
        Error = error;
    }

    public static Result Success { get; } = new(null);

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static Result<T> Ok<T>(T value) => Result<T>.CreateSuccess(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.CreateFailure(error);

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Resultado com valor em caso de sucesso.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null)
    {
        _value = value;
    }

    private Result(Error error)
        : base(error)
    {
    }

    /// <summary>Valor do sucesso. Acessar em uma falha é erro de programação.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Resultado com falha ({Error.Code}) não tem valor.");

    internal static Result<T> CreateSuccess(T value) => new(value);

    internal static Result<T> CreateFailure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Ok(map(Value)) : Failure<TOut>(Error);
    }

    public static implicit operator Result<T>(T value) => CreateSuccess(value);

    public static implicit operator Result<T>(Error error) => CreateFailure(error);
}

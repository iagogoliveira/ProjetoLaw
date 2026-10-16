using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.Http;

namespace LexFlow.Web.Common.Results;

/// <summary>Converte <see cref="Result"/> em respostas HTTP; falhas viram ProblemDetails (RFC 9457).</summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
    }

    /// <summary>200 com o valor no corpo.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result) => result.ToHttpResult(value => TypedResults.Ok(value));

    /// <summary>204 em sucesso.</summary>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
    }

    public static int ToStatusCode(this ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>
    /// ProblemDetails com <c>code</c> (chave de tradução no frontend) e, em validação, <c>errors</c> por campo:
    /// <c>{ "title": [{ "code": "case.title_required", "message": "..." }] }</c>. O <c>traceId</c> é incluído pelo
    /// <c>IProblemDetailsService</c> (ver <see cref="ProblemDetailsSetup"/>).
    /// </summary>
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = error.Code };
        if (error is ValidationError validation)
        {
            extensions["errors"] = validation.Errors
                .GroupBy(e => e.Field, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => new FieldProblem(e.Code, e.Message)).ToArray(), StringComparer.Ordinal);
        }

        var status = error.Type.ToStatusCode();
        return TypedResults.Problem(
            title: error.Message,
            statusCode: status,
            type: $"https://httpstatuses.io/{status}",
            extensions: extensions);
    }
}

/// <summary>Erro de um campo dentro de <c>errors</c>.</summary>
public sealed record FieldProblem(string Code, string Message);

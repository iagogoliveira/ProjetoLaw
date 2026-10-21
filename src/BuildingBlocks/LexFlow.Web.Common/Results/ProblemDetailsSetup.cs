using System.Diagnostics;
using LexFlow.Web.Common.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Web.Common.Results;

/// <summary>
/// ProblemDetails para todas as respostas de erro: falhas de negócio, 401/403/404 sem corpo e exceções.
/// Toda resposta leva <c>traceId</c> e <c>correlationId</c>; exceções nunca expõem mensagem ou stack.
/// </summary>
public static class ProblemDetailsSetup
{
    public const string UnexpectedErrorCode = "server.unexpected";

    public static IServiceCollection AddLexFlowProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            problem.Instance ??= context.HttpContext.Request.Path;
            problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
            if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
            {
                problem.Extensions["correlationId"] = correlationId;
            }

            if (!problem.Extensions.ContainsKey("code"))
            {
                problem.Extensions["code"] = problem.Status switch
                {
                    StatusCodes.Status400BadRequest => "http.bad_request",
                    StatusCodes.Status401Unauthorized => "auth.unauthenticated",
                    StatusCodes.Status403Forbidden => "auth.forbidden",
                    StatusCodes.Status404NotFound => "http.not_found",
                    StatusCodes.Status405MethodNotAllowed => "http.method_not_allowed",
                    StatusCodes.Status429TooManyRequests => "http.too_many_requests",
                    _ when context.Exception is not null => UnexpectedErrorCode,
                    _ => "http.error",
                };
            }

            if (context.Exception is not null)
            {
                problem.Title = context.Exception is BadHttpRequestException ? "A requisição é inválida." : "Ocorreu um erro inesperado.";
                problem.Detail = null;
            }
        });

    /// <summary>Exceções não tratadas e status sem corpo passam a responder com ProblemDetails.</summary>
    public static IApplicationBuilder UseLexFlowProblemDetails(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
                ? badRequest.StatusCode : StatusCodes.Status500InternalServerError,
        });
        app.UseStatusCodePages();
        return app;
    }
}

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LexFlow.Web.Common.Correlation;

/// <summary>Id de correlação da requisição atual, propagado para logs, ProblemDetails e eventos de integração.</summary>
public interface ICorrelationContext
{
    string CorrelationId { get; }
}

internal sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string CorrelationId =>
        accessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey] as string
        ?? Activity.Current?.TraceId.ToString()
        ?? string.Empty;
}

/// <summary>
/// Lê <c>X-Correlation-Id</c> (ou usa o TraceId) e devolve o mesmo cabeçalho na resposta. Valores recebidos só
/// são aceitos com até 64 caracteres de [A-Za-z0-9._-], para não permitir injeção em logs.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "lexflow.correlation_id";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsAcceptable(incoming)
            ? incoming
            : Activity.Current?.TraceId.ToString() ?? Guid.CreateVersion7().ToString("N");

        context.Items[ItemKey] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>(StringComparer.Ordinal) { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    internal static bool IsAcceptable(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= 64
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

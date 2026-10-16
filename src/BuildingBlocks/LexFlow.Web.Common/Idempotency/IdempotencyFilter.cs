using System.Security.Cryptography;
using System.Text;
using LexFlow.Application.Abstractions.Idempotency;
using LexFlow.SharedKernel.Results;
using LexFlow.Web.Common.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Web.Common.Idempotency;

public static class IdempotencyErrors
{
    public static readonly Error InvalidKey =
        Error.Validation("idempotency.invalid_key", "Idempotency-Key deve ter de 1 a 128 caracteres de [A-Za-z0-9._-].");

    public static readonly Error InProgress =
        Error.Conflict("idempotency.in_progress", "Uma requisição com esta Idempotency-Key ainda está em processamento.");

    public static readonly Error KeyReused =
        Error.BusinessRule("idempotency.key_reused", "Esta Idempotency-Key já foi usada com outro conteúdo.");
}

public static class IdempotencyEndpointExtensions
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    /// <summary>
    /// Torna o endpoint idempotente pelo cabeçalho <c>Idempotency-Key</c> (POST sensíveis: faturar, emitir NFS-e,
    /// gerar peça, importar). Sem o cabeçalho, a requisição é executada normalmente.
    /// </summary>
    public static TBuilder WithIdempotency<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new IdempotencyFilter());

    internal static bool IsValidKey(string key) =>
        key.Length is > 0 and <= 128 && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

/// <summary>Bufferiza o corpo quando há <c>Idempotency-Key</c>, para o filtro calcular o hash depois do binding.</summary>
internal sealed class IdempotencyBufferingMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey(IdempotencyEndpointExtensions.HeaderName))
        {
            context.Request.EnableBuffering();
        }

        return next(context);
    }
}

internal static class IdempotencyHash
{
    /// <summary>SHA-256 de método, caminho, query e corpo (hex, 64 caracteres).</summary>
    public static async Task<string> ComputeAsync(HttpRequest request)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes($"{request.Method}\n{request.Path}{request.QueryString}\n"));
        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, request.HttpContext.RequestAborted)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }

            request.Body.Position = 0;
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

internal sealed class IdempotencyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[IdempotencyEndpointExtensions.HeaderName].ToString();
        if (string.IsNullOrEmpty(key))
        {
            return await next(context);
        }

        if (!IdempotencyEndpointExtensions.IsValidKey(key))
        {
            return IdempotencyErrors.InvalidKey.ToProblem();
        }

        var store = http.RequestServices.GetRequiredService<IIdempotencyStore>();
        var begin = await store.BeginAsync(key, await IdempotencyHash.ComputeAsync(http.Request), http.RequestAborted);
        switch (begin.Outcome)
        {
            case IdempotencyOutcome.Replay:
                return new ReplayResult(begin.Response!);
            case IdempotencyOutcome.InProgress:
                return IdempotencyErrors.InProgress.ToProblem();
            case IdempotencyOutcome.KeyReused:
                return IdempotencyErrors.KeyReused.ToProblem();
        }

        object? result;
        try
        {
            result = await next(context);
        }
        catch
        {
            await store.AbandonAsync(key, CancellationToken.None);
            throw;
        }

        return new RecordingResult(result, key, store);
    }

    /// <summary>Executa a resposta original, grava status/tipo/corpo e a envia ao cliente.</summary>
    private sealed class RecordingResult(object? inner, string key, IIdempotencyStore store) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var original = httpContext.Response.Body;
            await using var buffer = new MemoryStream();
            httpContext.Response.Body = buffer;
            try
            {
                var result = inner as IResult ?? (inner is null ? TypedResults.Ok() : TypedResults.Ok(inner));
                await result.ExecuteAsync(httpContext);
            }
            finally
            {
                httpContext.Response.Body = original;
            }

            var status = httpContext.Response.StatusCode;
            if (status >= StatusCodes.Status500InternalServerError)
            {
                await store.AbandonAsync(key, CancellationToken.None);
            }
            else
            {
                var body = Encoding.UTF8.GetString(buffer.ToArray());
                await store.CompleteAsync(key, new StoredResponse(status, httpContext.Response.ContentType, body), CancellationToken.None);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(original, httpContext.RequestAborted);
        }
    }

    private sealed class ReplayResult(StoredResponse response) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = response.StatusCode;
            httpContext.Response.Headers[IdempotencyEndpointExtensions.ReplayedHeader] = "true";
            if (response.ContentType is not null)
            {
                httpContext.Response.ContentType = response.ContentType;
            }

            if (!string.IsNullOrEmpty(response.Body))
            {
                await httpContext.Response.WriteAsync(response.Body, httpContext.RequestAborted);
            }
        }
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexFlow.ServiceDefaults;

/// <summary>
/// Serializa o relatório de saúde com status e duração por dependência.
/// Não expõe mensagens de exceção nem dados de conexão.
/// </summary>
internal static class HealthReportWriter
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                tags = entry.Value.Tags,
            }),
        };

        context.Response.ContentType = "application/json; charset=utf-8";
        return JsonSerializer.SerializeAsync(context.Response.Body, payload, s_options, context.RequestAborted);
    }
}

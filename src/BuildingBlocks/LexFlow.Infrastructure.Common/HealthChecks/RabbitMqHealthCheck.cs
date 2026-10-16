using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace LexFlow.Infrastructure.Common.HealthChecks;

/// <summary>Abre e fecha uma conexão AMQP. Substituído pela conexão do Wolverine na Etapa 1.</summary>
internal sealed class RabbitMqHealthCheck(Uri connectionUri) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = connectionUri, RequestedConnectionTimeout = TimeSpan.FromSeconds(3) };
            await using var connection = await factory.CreateConnectionAsync("lexflow-health", cancellationToken);
            return connection.IsOpen ? HealthCheckResult.Healthy() : new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}

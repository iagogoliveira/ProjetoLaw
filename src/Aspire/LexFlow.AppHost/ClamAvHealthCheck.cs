using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LexFlow.AppHost;

/// <summary>
/// Health check do ClamAV: envia <c>zPING</c> pelo protocolo clamd e espera <c>PONG</c>.
/// O container só responde depois de carregar as assinaturas (pode levar alguns minutos).
/// </summary>
internal sealed class ClamAvHealthCheck(EndpointReference endpoint) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!endpoint.IsAllocated)
        {
            return HealthCheckResult.Unhealthy("Endpoint do ClamAV ainda não alocado.");
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(endpoint.Host, endpoint.Port, cancellationToken);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zPING\0"u8.ToArray(), cancellationToken);

            var buffer = new byte[16];
            var read = await stream.ReadAsync(buffer, cancellationToken);
            var reply = Encoding.ASCII.GetString(buffer, 0, read).TrimEnd('\0', '\n');

            return string.Equals(reply, "PONG", StringComparison.Ordinal)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Resposta inesperada do clamd.");
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            return HealthCheckResult.Unhealthy("ClamAV indisponível.", ex);
        }
    }
}

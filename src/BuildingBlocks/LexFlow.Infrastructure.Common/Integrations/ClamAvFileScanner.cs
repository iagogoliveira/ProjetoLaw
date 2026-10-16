using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using LexFlow.Application.Abstractions.Integrations;

namespace LexFlow.Infrastructure.Common.Integrations;

public sealed class ClamAvSettings
{
    public const string SectionName = "ClamAv";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 3310;
}

/// <summary>Resposta inesperada ou erro informado pelo clamd (ex.: tamanho acima do <c>StreamMaxLength</c>).</summary>
public sealed class FileScannerException(string message) : Exception(message);

/// <summary>
/// Cliente do protocolo clamd (comando <c>zINSTREAM</c>): envia o arquivo em blocos prefixados pelo tamanho (4 bytes,
/// big-endian) e um bloco vazio no fim. Respostas: <c>stream: OK</c> ou <c>stream: &lt;assinatura&gt; FOUND</c>.
/// Implementação própria em vez de pacote de terceiros (ADR 0009).
/// </summary>
internal sealed class ClamAvFileScanner(ClamAvSettings settings) : IFileScanner
{
    private const int ChunkSize = 64 * 1024;

    public async Task<FileScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var client = new TcpClient();
        await client.ConnectAsync(settings.Host, settings.Port, cancellationToken);
        await using var stream = client.GetStream();

        await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), cancellationToken);
        var buffer = new byte[ChunkSize];
        var length = new byte[4];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            BinaryPrimitives.WriteInt32BigEndian(length, read);
            await stream.WriteAsync(length, cancellationToken);
            await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        BinaryPrimitives.WriteInt32BigEndian(length, 0);
        await stream.WriteAsync(length, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        using var reply = new MemoryStream();
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            reply.Write(buffer, 0, read);
        }

        return Parse(Encoding.ASCII.GetString(reply.ToArray()).TrimEnd('\0', '\n'));
    }

    internal static FileScanResult Parse(string reply)
    {
        if (reply.EndsWith(" OK", StringComparison.Ordinal))
        {
            return FileScanResult.Clean;
        }

        if (reply.EndsWith(" FOUND", StringComparison.Ordinal))
        {
            var signature = reply["stream: ".Length..^" FOUND".Length];
            return FileScanResult.Infected(signature);
        }

        throw new FileScannerException($"Resposta inesperada do ClamAV: {reply}");
    }
}

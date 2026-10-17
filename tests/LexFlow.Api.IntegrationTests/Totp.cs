using System.Buffers.Binary;
using System.Security.Cryptography;

namespace LexFlow.Api.IntegrationTests;

/// <summary>Gerador TOTP (RFC 6238: HMAC-SHA1, passo de 30 s, 6 dígitos), como um aplicativo autenticador faria.</summary>
internal static class Totp
{
    public static string Code(string base32Key, DateTimeOffset at)
    {
        var step = at.ToUnixTimeSeconds() / 30;
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
#pragma warning disable CA5350 // o RFC 6238 usa HMAC-SHA1, como os aplicativos autenticadores
        var hash = HMACSHA1.HashData(FromBase32(base32Key), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var binary = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7FFFFFFF) % 1_000_000;
        return binary.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] FromBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in input.TrimEnd('=').ToUpperInvariant())
        {
            value = (value << 5) | alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. output];
    }
}

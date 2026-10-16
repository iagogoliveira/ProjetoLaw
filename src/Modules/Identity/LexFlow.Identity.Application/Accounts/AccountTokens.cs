using System.Buffers.Text;
using System.Text;

namespace LexFlow.Identity.Application.Accounts;

/// <summary>
/// Tokens do Identity (confirmação de e-mail, redefinição de senha) em Base64Url, para trafegar em links sem
/// escape. O Worker codifica ao montar o link; os handlers decodificam o que chega na API.
/// </summary>
public static class AccountTokens
{
    public static string Encode(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));
    }

    /// <summary>Nulo quando o valor não é Base64Url válido (link adulterado ou truncado).</summary>
    public static string? Decode(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded) || !Base64Url.IsValid(encoded))
        {
            return null;
        }

        return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));
    }
}

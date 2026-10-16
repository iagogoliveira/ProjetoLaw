using System.Security.Cryptography;
using System.Text;
using LexFlow.Identity.Application.Accounts;
using LexFlow.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace LexFlow.Identity.Infrastructure.Passwords;

/// <summary>
/// Rejeita senhas que aparecem em vazamentos (API Pwned Passwords, k-anonymity): só os 5 primeiros caracteres do
/// SHA-1 saem do servidor, e a resposta vem com preenchimento para não revelar o tamanho. Se a API falhar, a senha
/// é aceita: a verificação reforça a política, mas não pode impedir cadastro ou troca de senha.
/// </summary>
internal sealed partial class PwnedPasswordValidator(HttpClient http, ILogger<PwnedPasswordValidator> logger) : IPasswordValidator<ApplicationUser>
{
    public static readonly Uri BaseAddress = new("https://api.pwnedpasswords.com/");

    private const int PrefixLength = 5;

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return IdentityResult.Success;
        }

        // O protocolo da API usa SHA-1; o hash não é armazenado nem usado como proteção da senha.
#pragma warning disable CA5350
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
#pragma warning restore CA5350

        string body;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"range/{hash[..PrefixLength]}", UriKind.Relative));
            request.Headers.Add("Add-Padding", "true");
            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            body = await response.Content.ReadAsStringAsync();
        }
#pragma warning disable CA1031 // falha de rede, timeout ou resiliência esgotada: a verificação é aberta por decisão
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogUnavailable(logger, ex);
            return IdentityResult.Success;
        }

        return IsBreached(body, hash[PrefixLength..])
            ? IdentityResult.Failed(new IdentityError { Code = PasswordPolicy.BreachedErrorCode, Description = "Senha encontrada em vazamentos conhecidos." })
            : IdentityResult.Success;
    }

    /// <summary>Linhas <c>SUFIXO:CONTAGEM</c>; as de preenchimento têm contagem 0.</summary>
    internal static bool IsBreached(string body, string suffix)
    {
        foreach (var line in body.Split('\n'))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator > 0
                && line.AsSpan(0, separator).Equals(suffix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.AsSpan(separator + 1).Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count)
                && count > 0)
            {
                return true;
            }
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Verificação de senhas vazadas indisponível; senha aceita sem a verificação")]
    private static partial void LogUnavailable(ILogger logger, Exception exception);
}

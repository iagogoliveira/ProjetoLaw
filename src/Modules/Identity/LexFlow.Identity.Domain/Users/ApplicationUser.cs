using Microsoft.AspNetCore.Identity;

namespace LexFlow.Identity.Domain.Users;

/// <summary>Segundo fator escolhido pelo usuário (MFA opcional nesta etapa, decisão de 2026-10-16).</summary>
public enum MfaMethod
{
    None,

    /// <summary>Código de aplicativo autenticador (TOTP, RFC 6238).</summary>
    Totp,

    /// <summary>Código enviado por e-mail a cada login.</summary>
    Email,
}

/// <summary>
/// Usuário da plataforma (ASP.NET Core Identity, tabela <c>identity.users</c>). É global, sem <c>OrganizationId</c>:
/// a mesma pessoa pode pertencer a vários escritórios (RF-02); a pertença e os perfis ficam no módulo Organization (Etapa 3).
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int MaxFullNameLength = 200;

    private ApplicationUser()
    {
    }

    public string FullName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public MfaMethod MfaMethod { get; private set; }

    /// <summary>Novo usuário com e-mail ainda não confirmado; o e-mail é também o nome de login.</summary>
    public static ApplicationUser Register(Guid id, string email, string fullName, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        return new ApplicationUser
        {
            Id = id,
            Email = email,
            UserName = email,
            FullName = fullName.Trim(),
            CreatedAt = now,
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
    }

    public void Rename(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        FullName = fullName.Trim();
    }

    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    /// <summary>Liga o segundo fator; o login passa a pedi-lo sempre (sem "lembrar este dispositivo").</summary>
    public void EnableMfa(MfaMethod method)
    {
        if (method == MfaMethod.None)
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Escolha TOTP ou e-mail.");
        }

        MfaMethod = method;
        TwoFactorEnabled = true;
    }
}

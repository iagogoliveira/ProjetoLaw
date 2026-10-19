using System.Security.Cryptography;
using LexFlow.Application.Abstractions;
using LexFlow.Publications.Application.Movements;
using LexFlow.SharedKernel.Results;
using Microsoft.AspNetCore.DataProtection;

namespace LexFlow.Publications.Infrastructure.Movements;

internal sealed class MovementSecrets(IDataProtectionProvider protection, ITenantContext tenant) : IMovementSecrets
{
    private IDataProtector Protector => protection.CreateProtector("LexFlow.Publications.DataJud.v1", tenant.OrganizationId?.ToString("N")
        ?? throw new InvalidOperationException("Credenciais exigem tenant ativo."));
    public string Protect(string value) => Protector.Protect(value);
    public Result<string> Unprotect(string value)
    {
        try { return Protector.Unprotect(value); }
        catch (CryptographicException) { return Error.BusinessRule("publications.credential_unreadable", "Não foi possível abrir a credencial da integração."); }
    }
}

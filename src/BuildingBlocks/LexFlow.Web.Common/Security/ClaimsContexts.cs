using System.Security.Claims;
using LexFlow.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace LexFlow.Web.Common.Security;

/// <summary>Nomes das claims do access token (seção 7.1).</summary>
public static class LexFlowClaims
{
    public const string Subject = "sub";
    public const string OrganizationId = "org_id";
    public const string SessionId = "sid";
}

/// <summary>Usuário a partir da claim <c>sub</c> do token.</summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid? UserId => IsAuthenticated ? ParseGuid(accessor.HttpContext!.User, LexFlowClaims.Subject) : null;

    internal static Guid? ParseGuid(ClaimsPrincipal user, string claimType) =>
        Guid.TryParse(user.FindFirstValue(claimType), out var id) ? id : null;
}

/// <summary>
/// Organização ativa a partir da claim <c>org_id</c>. Provisório: a validação contra <c>OrganizationMember</c>
/// e a troca de organização (cabeçalho <c>X-Organization-Id</c>) chegam na Etapa 3.
/// </summary>
internal sealed class HttpContextTenantContext(IHttpContextAccessor accessor, TenantOverride tenantOverride) : ITenantContext
{
    public Guid? OrganizationId =>
        tenantOverride.OrganizationId
        ?? (accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? HttpContextCurrentUser.ParseGuid(user, LexFlowClaims.OrganizationId)
            : null);
}

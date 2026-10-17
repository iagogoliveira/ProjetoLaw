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
    public const string AuthenticationMethods = "amr";
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
/// Organização ativa já validada contra a pertença pelo <see cref="TenantResolutionMiddleware"/> (seção 6.2, item 2).
/// O <see cref="TenantOverride"/> do onboarding tem precedência.
/// </summary>
internal sealed class HttpContextTenantContext(IHttpContextAccessor accessor, TenantOverride tenantOverride) : ITenantContext
{
    public Guid? OrganizationId =>
        tenantOverride.OrganizationId
        ?? (accessor.HttpContext?.Items.TryGetValue(TenantResolutionMiddleware.ItemKey, out var value) == true ? value as Guid? : null);
}

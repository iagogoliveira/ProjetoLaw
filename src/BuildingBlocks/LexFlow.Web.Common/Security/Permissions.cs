using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace LexFlow.Web.Common.Security;

/// <summary>Permissão exigida pelo endpoint (<c>modulo.recurso.acao</c>), lida pelos SecurityTests e pela tela de perfis.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiredPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

public static class PermissionEndpointExtensions
{
    public const string PolicyPrefix = "permission:";

    /// <summary>
    /// Exige a permissão no endpoint (seção 7.2, passo 1). Até a Etapa 3 a política só exige usuário autenticado;
    /// o nome da permissão já fica nos metadados para o <c>PermissionService</c> e os SecurityTests.
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        return builder
            .WithMetadata(new RequiredPermissionAttribute(permission))
            .RequireAuthorization(PolicyPrefix + permission);
    }
}

/// <summary>
/// Provedor dinâmico de políticas <c>permission:*</c>. Placeholder da Etapa 1: exige só autenticação.
/// Na Etapa 3 passa a consultar o <c>PermissionService</c> (perfil + escopo, cache com invalidação por evento).
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    private static readonly AuthorizationPolicy AuthenticatedOnly = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        policyName.StartsWith(PermissionEndpointExtensions.PolicyPrefix, StringComparison.Ordinal)
            ? Task.FromResult<AuthorizationPolicy?>(AuthenticatedOnly)
            : base.GetPolicyAsync(policyName);
}

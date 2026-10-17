using LexFlow.Application.Abstractions;
using LexFlow.Application.Abstractions.Security;
using LexFlow.Web.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LexFlow.Web.Common.Security;

/// <summary>Permissão exigida pelo endpoint (<c>modulo.recurso.acao</c>), lida pelos SecurityTests e pela tela de perfis.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequiredPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Endpoint público, sem login (ex.: cadastro, recuperação de senha). A exceção à regra "todo endpoint tem permissão"
/// fica explícita e com motivo, para os SecurityTests distinguirem esquecimento de decisão.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PublicEndpointAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
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

    /// <summary>Libera o endpoint sem autenticação, registrando o motivo nos metadados (<see cref="PublicEndpointAttribute"/>).</summary>
    public static TBuilder AllowPublicAccess<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return builder
            .WithMetadata(new PublicEndpointAttribute(reason))
            .AllowAnonymous();
    }
}

/// <summary>Exige a permissão nomeada na organização ativa.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Provedor dinâmico de políticas <c>permission:*</c>: usuário autenticado + <see cref="PermissionRequirement"/>.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        ArgumentNullException.ThrowIfNull(policyName);
        if (!policyName.StartsWith(PermissionEndpointExtensions.PolicyPrefix, StringComparison.Ordinal))
        {
            return base.GetPolicyAsync(policyName);
        }

        var permission = policyName[PermissionEndpointExtensions.PolicyPrefix.Length..];
        return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build());
    }
}

/// <summary>
/// Primeira etapa da autorização (seção 7.2): consulta o <see cref="IPermissionChecker"/> (perfil do membro na
/// organização ativa, com cache e invalidação por evento). Sem verificador registrado, nega.
/// </summary>
internal sealed class PermissionAuthorizationHandler(IPermissionChecker? checker, ITenantContext tenant) : AuthorizationHandler<PermissionRequirement>
{
    public const string MfaRequiredReason = "organization.mfa_required";

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);
        if (checker is null || HttpContextCurrentUser.ParseGuid(context.User, LexFlowClaims.Subject) is not { } userId)
        {
            return;
        }

        var mfa = context.User.FindAll(LexFlowClaims.AuthenticationMethods).Any(c => string.Equals(c.Value, "mfa", StringComparison.Ordinal));
        var decision = await checker.CheckAsync(new PermissionCheck(userId, tenant.OrganizationId, requirement.Permission, mfa), CancellationToken.None);
        switch (decision)
        {
            case PermissionDecision.Allowed:
                context.Succeed(requirement);
                break;
            case PermissionDecision.MfaRequired:
                context.Fail(new AuthorizationFailureReason(this, MfaRequiredReason));
                break;
        }
    }
}

/// <summary>403 por MFA exigido sai com o código próprio, para o SPA mandar o usuário ativar o segundo fator.</summary>
internal sealed class LexFlowAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(authorizeResult);
        return authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailureReasons.Any(r => string.Equals(r.Message, PermissionAuthorizationHandler.MfaRequiredReason, StringComparison.Ordinal)) == true
            ? SecurityProblems.MfaRequired.ToProblem().ExecuteAsync(context)
            : _default.HandleAsync(next, context, policy, authorizeResult);
    }
}

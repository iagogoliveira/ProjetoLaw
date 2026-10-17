using LexFlow.Application.Abstractions;
using LexFlow.Web.Common.Correlation;
using LexFlow.Web.Common.Idempotency;
using LexFlow.Web.Common.Results;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexFlow.Web.Common;

public static class DependencyInjection
{
    /// <summary>
    /// ProblemDetails, correlação, autenticação provisória (401 até a Etapa 2), políticas de permissão e
    /// <see cref="ICurrentUser"/>/<see cref="ITenantContext"/> a partir das claims.
    /// </summary>
    public static IServiceCollection AddWebCommon(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddLexFlowProblemDetails();
        services.TryAddScoped<IRequestContext, HttpRequestContext>();
        services.TryAddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.TryAddScoped<TenantOverride>();
        services.TryAddScoped<ITenantContext, HttpContextTenantContext>();

        services.AddAuthentication(UnconfiguredAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, UnconfiguredAuthenticationHandler>(UnconfiguredAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        return services;
    }

    /// <summary>Ordem: ProblemDetails → correlação → buffer de idempotência → autenticação → autorização.</summary>
    public static IApplicationBuilder UseWebCommon(this IApplicationBuilder app)
    {
        app.UseLexFlowProblemDetails();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<IdempotencyBufferingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}

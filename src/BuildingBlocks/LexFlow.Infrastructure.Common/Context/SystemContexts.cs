using LexFlow.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexFlow.Infrastructure.Common.Context;

/// <summary>Processo sem usuário (migrations, jobs de plataforma): grava <c>SystemUser.Id</c> em created_by.</summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public Guid? UserId => null;
}

/// <summary>Sem organização ativa: filtros de tenant não retornam linhas e gravações de escritório são rejeitadas.</summary>
internal sealed class NoTenantContext : ITenantContext
{
    public Guid? OrganizationId => null;
}

internal sealed class NoRequestContext : IRequestContext
{
    public string? CorrelationId => null;

    public string? IpAddress => null;

    public string? UserAgent => null;
}

public static class SystemContextRegistration
{
    /// <summary>Contextos de usuário, tenant e requisição para processos fora de HTTP. Não sobrescreve registros existentes.</summary>
    public static IServiceCollection AddSystemContexts(this IServiceCollection services)
    {
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.TryAddScoped<ITenantContext, NoTenantContext>();
        services.TryAddScoped<IRequestContext, NoRequestContext>();
        return services;
    }
}

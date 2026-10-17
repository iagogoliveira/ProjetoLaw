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

public static class SystemContextRegistration
{
    /// <summary>
    /// Contextos para processos fora de HTTP: usuário de sistema e tenant/correlação do escopo ambiente
    /// (<see cref="AmbientContext"/>); sem escopo aberto, não há tenant. Não sobrescreve registros existentes.
    /// </summary>
    public static IServiceCollection AddSystemContexts(this IServiceCollection services)
    {
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.TryAddScoped<TenantOverride>();
        services.TryAddScoped<ITenantContext, AmbientTenantContext>();
        services.TryAddScoped<IRequestContext, AmbientRequestContext>();
        return services;
    }
}

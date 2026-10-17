using LexFlow.Organization.Contracts.Permissions;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Regra do CLAUDE.md, verificada sobre todos os endpoints da API: cada um exige permissão ou é público com motivo
/// declarado, nunca as duas coisas e nunca nenhuma. Endpoints novos entram sozinhos (base dos SecurityTests da Etapa 3).
/// </summary>
public sealed class EndpointSecurityConventionTests(DependenciesFixture dependencies)
{
    [Fact]
    public async Task Every_api_endpoint_requires_a_permission_or_is_explicitly_public()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .ToList();

        endpoints.ShouldNotBeEmpty();
        var violations = endpoints
            .Where(e => e.Metadata.GetMetadata<RequiredPermissionAttribute>() is null == e.Metadata.GetMetadata<PublicEndpointAttribute>() is null)
            .Select(e => e.RoutePattern.RawText)
            .ToList();
        violations.ShouldBeEmpty();
    }

    /// <summary>Permissão usada num endpoint e ausente do catálogo nunca apareceria na tela de perfis.</summary>
    [Fact]
    public async Task Every_required_permission_is_in_the_catalog()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);

        var unknown = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .SelectMany(e => e.Metadata.GetOrderedMetadata<RequiredPermissionAttribute>())
            .Select(p => p.Permission)
            .Where(p => !PermissionCatalog.Exists(p))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        unknown.ShouldBeEmpty();
    }
}

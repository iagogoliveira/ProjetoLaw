using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Web.Common.Modules;

/// <summary>
/// Ponto de entrada de um módulo (seção 4.2): registra serviços e mapeia endpoints. O <c>Program.cs</c> só lista os módulos.
/// </summary>
public interface IModule
{
    void AddServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}

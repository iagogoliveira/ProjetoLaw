using System.Reflection;
using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace LexFlow.ArchitectureTests;

/// <summary>
/// Regras de dependência da seção 4.2 aplicadas por namespace (<c>{Prefixo}.{Modulo}.{Camada}</c>).
/// O prefixo é parâmetro para que as regras possam ser testadas contra o fixture <c>ArchFixture.*</c>.
/// </summary>
internal static class LayerRules
{
    public const string Domain = "Domain";
    public const string Application = "Application";
    public const string Infrastructure = "Infrastructure";
    public const string Contracts = "Contracts";
    public const string Endpoints = "Endpoints";

    public static readonly string[] Layers = [Domain, Application, Infrastructure, Contracts, Endpoints];

    /// <summary>Camadas do mesmo módulo que cada camada não pode usar.</summary>
    private static readonly Dictionary<string, string[]> ForbiddenInSameModule = new(StringComparer.Ordinal)
    {
        [Domain] = [Application, Infrastructure, Contracts, Endpoints],
        [Contracts] = [Domain, Application, Infrastructure, Endpoints],
        [Application] = [Infrastructure, Endpoints],
        [Infrastructure] = [Endpoints],
        [Endpoints] = [Infrastructure],
    };

    /// <summary>BuildingBlocks (sem o prefixo) que cada camada não pode usar.</summary>
    private static readonly Dictionary<string, string[]> ForbiddenBuildingBlocks = new(StringComparer.Ordinal)
    {
        [Domain] = ["Application.Abstractions", "Infrastructure.Common", "Web.Common"],
        [Contracts] = ["Application.Abstractions", "Infrastructure.Common", "Web.Common"],
        [Application] = ["Infrastructure.Common", "Web.Common"],
        [Infrastructure] = ["Web.Common"],
        [Endpoints] = ["Infrastructure.Common"],
    };

    /// <summary>Módulos encontrados nos namespaces dos assemblies (ex.: <c>LexFlow.Crm.Domain</c> → <c>Crm</c>).</summary>
    public static IReadOnlyList<string> DiscoverModules(IEnumerable<Assembly> assemblies, string prefix)
    {
        var pattern = new Regex($@"^{Regex.Escape(prefix)}\.(?<module>[A-Za-z0-9]+)\.(?:{string.Join('|', Layers)})(?:\.|$)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        return [.. assemblies
            .SelectMany(a => a.GetTypes())
            .Select(t => t.Namespace is null ? null : pattern.Match(t.Namespace))
            .Where(m => m is { Success: true })
            .Select(m => m!.Groups["module"].Value)
            .Where(m => !IsBuildingBlockRoot(m))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>Retorna uma linha por violação: <c>regra: tipo</c>. Lista vazia = arquitetura conforme.</summary>
    public static IReadOnlyList<string> Check(IReadOnlyCollection<Assembly> assemblies, string prefix)
    {
        var modules = DiscoverModules(assemblies, prefix);
        var violations = new List<string>();

        foreach (var module in modules)
        {
            foreach (var layer in Layers)
            {
                var forbidden = ForbiddenInSameModule[layer].Select(l => $"{prefix}.{module}.{l}")
                    .Concat(ForbiddenBuildingBlocks[layer].Select(b => $"{prefix}.{b}"))
                    .Concat(modules.Where(other => other != module)
                        .SelectMany(other => Layers.Where(l => l != Contracts).Select(l => $"{prefix}.{other}.{l}")))
                    .ToArray();

                var result = Types.InAssemblies(assemblies)
                    .That().ResideInNamespaceMatching($@"^{Regex.Escape($"{prefix}.{module}.{layer}")}(\..*)?$")
                    .ShouldNot().HaveDependencyOnAny(forbidden)
                    .GetResult();

                if (!result.IsSuccessful)
                {
                    violations.AddRange(result.FailingTypes.Select(t => $"{module}.{layer} viola a seção 4.2: {t.FullName}"));
                }
            }
        }

        return violations;
    }

    private static bool IsBuildingBlockRoot(string module) =>
        module is "SharedKernel" or "Application" or "Infrastructure" or "Web";
}

using System.Reflection;
using System.Text.RegularExpressions;

namespace LexFlow.ArchitectureTests;

/// <summary>
/// Descobre os assemblies LexFlow copiados para a saída do teste e os classifica
/// em building blocks e camadas de módulo (<c>LexFlow.{Modulo}.{Camada}</c>).
/// </summary>
internal static partial class LexFlowAssemblies
{
    public const string SharedKernelName = "LexFlow.SharedKernel";
    public const string ApplicationAbstractionsName = "LexFlow.Application.Abstractions";
    public const string InfrastructureCommonName = "LexFlow.Infrastructure.Common";
    public const string WebCommonName = "LexFlow.Web.Common";

    public static readonly IReadOnlyList<string> BuildingBlocks =
        [SharedKernelName, ApplicationAbstractionsName, InfrastructureCommonName, WebCommonName];

    public static readonly IReadOnlyList<Assembly> All = Directory
        .EnumerateFiles(AppContext.BaseDirectory, "LexFlow.*.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(name => !string.Equals(name, typeof(LexFlowAssemblies).Assembly.GetName().Name, StringComparison.Ordinal))
        .Select(name => Assembly.Load(name!))
        .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
        .ToList();

    public static readonly IReadOnlyList<string> AllNames = All.Select(Name).ToList();

    public static readonly IReadOnlyList<ModuleAssembly> Modules = All
        .Select(a => (Assembly: a, Match: ModuleNamePattern().Match(Name(a))))
        .Where(x => x.Match.Success)
        .Select(x => new ModuleAssembly(x.Assembly, x.Match.Groups["module"].Value, Enum.Parse<ModuleLayer>(x.Match.Groups["layer"].Value)))
        .ToList();

    public static Assembly Get(string name) =>
        All.Single(a => string.Equals(Name(a), name, StringComparison.Ordinal));

    public static string Name(Assembly assembly) => assembly.GetName().Name!;

    public static string LayerAssemblyName(string module, ModuleLayer layer) => $"LexFlow.{module}.{layer}";

    [GeneratedRegex(@"^LexFlow\.(?<module>[A-Za-z]+)\.(?<layer>Domain|Application|Infrastructure|Contracts|Endpoints)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModuleNamePattern();
}

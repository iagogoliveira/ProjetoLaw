using System.Reflection;

namespace LexFlow.ArchitectureTests;

/// <summary>Todos os assemblies de produção <c>LexFlow.*</c> copiados para o bin dos testes.</summary>
internal static class LexFlowAssemblies
{
    public const string Prefix = "LexFlow";

    private static readonly Lazy<IReadOnlyList<Assembly>> LazyAll = new(Load);

    public static IReadOnlyList<Assembly> All => LazyAll.Value;

    public static Assembly Get(string name) =>
        All.SingleOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"Assembly '{name}' não encontrado no bin dos testes.");

    private static IReadOnlyList<Assembly> Load() =>
        [.. Directory.EnumerateFiles(AppContext.BaseDirectory, $"{Prefix}.*.dll")
            .Where(path => !Path.GetFileName(path).Contains("Tests", StringComparison.Ordinal))
            .Select(path => Assembly.LoadFrom(path))
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)];
}

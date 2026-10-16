using System.Reflection;
using NetArchTest.Rules;

namespace LexFlow.ArchitectureTests;

/// <summary>Verificação de dependências por tipo (NetArchTest) entre assemblies LexFlow.</summary>
internal static class DependencyRules
{
    /// <summary>
    /// Falha se algum tipo de <paramref name="assembly"/> usar tipos de um assembly LexFlow
    /// que não esteja em <paramref name="allowed"/> (o próprio assembly é sempre permitido).
    /// </summary>
    public static string? CheckDependsOnlyOn(Assembly assembly, IEnumerable<string> allowed)
    {
        var self = LexFlowAssemblies.Name(assembly);
        var allowedSet = allowed.Append(self).ToHashSet(StringComparer.Ordinal);
        var forbidden = LexFlowAssemblies.AllNames.Where(n => !allowedSet.Contains(n)).ToArray();
        return CheckDoesNotDependOn(assembly, forbidden);
    }

    /// <summary>Falha se algum tipo de <paramref name="assembly"/> usar tipos dos namespaces proibidos.</summary>
    public static string? CheckDoesNotDependOn(Assembly assembly, IReadOnlyCollection<string> forbidden)
    {
        if (forbidden.Count == 0)
        {
            return null;
        }

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny([.. forbidden])
            .GetResult();

        if (result.IsSuccessful)
        {
            return null;
        }

        var offenders = string.Join(", ", result.FailingTypeNames ?? []);
        return $"{LexFlowAssemblies.Name(assembly)} depende de [{string.Join(", ", forbidden)}] em: {offenders}";
    }

    public static void ShouldHaveNoViolations(this IEnumerable<string?> violations)
    {
        var list = violations.OfType<string>().ToList();
        list.ShouldBeEmpty(string.Join(Environment.NewLine, list));
    }
}

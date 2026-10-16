using static LexFlow.ArchitectureTests.LexFlowAssemblies;

namespace LexFlow.ArchitectureTests;

/// <summary>Regras dos building blocks (seção 4.1/4.2 de docs/spec/implementacao.md).</summary>
public sealed class BuildingBlockTests
{
    [Fact]
    public void AssemblyDiscovery_FindsBuildingBlocks()
    {
        foreach (var name in BuildingBlocks)
        {
            AllNames.ShouldContain(name, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void SharedKernel_HasNoLexFlowDependencies() =>
        new[] { DependencyRules.CheckDependsOnlyOn(Get(SharedKernelName), []) }.ShouldHaveNoViolations();

    [Fact]
    public void ApplicationAbstractions_DependsOnlyOnSharedKernel() =>
        new[] { DependencyRules.CheckDependsOnlyOn(Get(ApplicationAbstractionsName), [SharedKernelName]) }.ShouldHaveNoViolations();

    [Fact]
    public void InfrastructureCommon_DependsOnlyOnAbstractions() =>
        new[] { DependencyRules.CheckDependsOnlyOn(Get(InfrastructureCommonName), [SharedKernelName, ApplicationAbstractionsName]) }.ShouldHaveNoViolations();

    [Fact]
    public void WebCommon_DoesNotDependOnInfrastructure()
    {
        var infrastructure = AllNames
            .Where(n => n.EndsWith(".Infrastructure", StringComparison.Ordinal) || string.Equals(n, InfrastructureCommonName, StringComparison.Ordinal))
            .ToList();

        new[] { DependencyRules.CheckDoesNotDependOn(Get(WebCommonName), infrastructure) }.ShouldHaveNoViolations();
    }

    [Fact]
    public void BuildingBlocks_DoNotDependOnModules()
    {
        var modules = Modules.Select(m => m.Name).ToList();

        BuildingBlocks
            .Select(name => DependencyRules.CheckDoesNotDependOn(Get(name), modules))
            .ShouldHaveNoViolations();
    }
}

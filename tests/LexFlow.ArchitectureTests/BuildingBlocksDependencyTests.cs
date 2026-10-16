using NetArchTest.Rules;

namespace LexFlow.ArchitectureTests;

/// <summary>Dependências entre os BuildingBlocks (seção 4.1 e 4.2).</summary>
public sealed class BuildingBlocksDependencyTests
{
    private const string SharedKernel = "LexFlow.SharedKernel";
    private const string ApplicationAbstractions = "LexFlow.Application.Abstractions";
    private const string InfrastructureCommon = "LexFlow.Infrastructure.Common";
    private const string WebCommon = "LexFlow.Web.Common";

    [Fact]
    public void All_building_blocks_are_discovered() =>
        new[] { SharedKernel, ApplicationAbstractions, InfrastructureCommon, WebCommon, "LexFlow.Api", "LexFlow.Worker" }
            .ShouldBeSubsetOf(LexFlowAssemblies.All.Select(a => a.GetName().Name!));

    [Fact]
    public void SharedKernel_does_not_depend_on_other_LexFlow_projects() =>
        AssertNoDependency(SharedKernel, ApplicationAbstractions, InfrastructureCommon, WebCommon, "LexFlow.Api", "LexFlow.Worker");

    [Fact]
    public void ApplicationAbstractions_depends_only_on_SharedKernel() =>
        AssertNoDependency(ApplicationAbstractions, InfrastructureCommon, WebCommon, "LexFlow.Api", "LexFlow.Worker");

    [Fact]
    public void InfrastructureCommon_does_not_depend_on_web_or_hosts() =>
        AssertNoDependency(InfrastructureCommon, WebCommon, "LexFlow.Api", "LexFlow.Worker");

    [Fact]
    public void WebCommon_does_not_depend_on_infrastructure_or_hosts() =>
        AssertNoDependency(WebCommon, InfrastructureCommon, "LexFlow.Api", "LexFlow.Worker");

    [Fact]
    public void Building_blocks_do_not_depend_on_modules()
    {
        var modules = LayerRules.DiscoverModules(LexFlowAssemblies.All, LexFlowAssemblies.Prefix);
        if (modules.Count == 0)
        {
            return; // nenhum módulo ainda (Etapa 0); a regra passa a valer quando o primeiro surgir
        }

        foreach (var buildingBlock in new[] { SharedKernel, ApplicationAbstractions, InfrastructureCommon, WebCommon })
        {
            AssertNoDependency(buildingBlock, [.. modules.Select(m => $"{LexFlowAssemblies.Prefix}.{m}")]);
        }
    }

    private static void AssertNoDependency(string assemblyName, params string[] forbidden)
    {
        var result = Types.InAssembly(LexFlowAssemblies.Get(assemblyName))
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"{assemblyName} não pode depender de {string.Join(", ", forbidden)}. Tipos em violação: " +
            string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }
}

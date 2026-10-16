using static LexFlow.ArchitectureTests.LexFlowAssemblies;

namespace LexFlow.ArchitectureTests;

/// <summary>
/// Fronteiras de módulo (tabela da seção 4.2). Os módulos são descobertos pelo nome
/// <c>LexFlow.{Modulo}.{Camada}</c>; enquanto não houver módulos, as regras não têm o que verificar.
/// </summary>
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Domain_DependsOnlyOnSharedKernel() =>
        Layer(ModuleLayer.Domain)
            .Select(m => DependencyRules.CheckDependsOnlyOn(m.Assembly, [SharedKernelName]))
            .ShouldHaveNoViolations();

    [Fact]
    public void Contracts_DependOnlyOnSharedKernel() =>
        Layer(ModuleLayer.Contracts)
            .Select(m => DependencyRules.CheckDependsOnlyOn(m.Assembly, [SharedKernelName]))
            .ShouldHaveNoViolations();

    [Fact]
    public void Application_DoesNotReferenceInfrastructure() =>
        Layer(ModuleLayer.Application)
            .Select(m => DependencyRules.CheckDependsOnlyOn(
                m.Assembly,
                [SharedKernelName, ApplicationAbstractionsName, Own(m, ModuleLayer.Domain), .. OtherContracts(m)]))
            .ShouldHaveNoViolations();

    [Fact]
    public void Infrastructure_DependsOnlyOnOwnModuleAndCommon() =>
        Layer(ModuleLayer.Infrastructure)
            .Select(m => DependencyRules.CheckDependsOnlyOn(
                m.Assembly,
                [SharedKernelName, ApplicationAbstractionsName, InfrastructureCommonName,
                 Own(m, ModuleLayer.Domain), Own(m, ModuleLayer.Application), Own(m, ModuleLayer.Contracts), .. OtherContracts(m)]))
            .ShouldHaveNoViolations();

    [Fact]
    public void Endpoints_DoNotReferenceInfrastructure() =>
        Layer(ModuleLayer.Endpoints)
            .Select(m => DependencyRules.CheckDependsOnlyOn(
                m.Assembly,
                [SharedKernelName, ApplicationAbstractionsName, WebCommonName,
                 Own(m, ModuleLayer.Application), Own(m, ModuleLayer.Contracts)]))
            .ShouldHaveNoViolations();

    [Fact]
    public void Module_DoesNotReferenceOtherModuleInternals() =>
        Modules
            .Select(m => DependencyRules.CheckDoesNotDependOn(
                m.Assembly,
                Modules.Where(o => !string.Equals(o.Module, m.Module, StringComparison.Ordinal) && o.Layer != ModuleLayer.Contracts)
                       .Select(o => o.Name)
                       .ToList()))
            .ShouldHaveNoViolations();

    private static IEnumerable<ModuleAssembly> Layer(ModuleLayer layer) => Modules.Where(m => m.Layer == layer);

    private static string Own(ModuleAssembly module, ModuleLayer layer) => LayerAssemblyName(module.Module, layer);

    private static IEnumerable<string> OtherContracts(ModuleAssembly module) =>
        Modules.Where(o => o.Layer == ModuleLayer.Contracts && !string.Equals(o.Module, module.Module, StringComparison.Ordinal))
               .Select(o => o.Name);
}

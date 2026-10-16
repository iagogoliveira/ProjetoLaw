namespace LexFlow.ArchitectureTests;

/// <summary>Regras da seção 4.2 aplicadas a todos os módulos <c>LexFlow.{Modulo}.{Camada}</c> existentes.</summary>
public sealed class ModuleDependencyTests
{
    [Fact]
    public void Module_layers_respect_dependency_rules()
    {
        var violations = LayerRules.Check([.. LexFlowAssemblies.All], LexFlowAssemblies.Prefix);

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    /// <summary>Garante que as regras acima não passam por vacuidade: os módulos existentes são encontrados.</summary>
    [Fact]
    public void Existing_modules_are_discovered() =>
        LayerRules.DiscoverModules(LexFlowAssemblies.All, LexFlowAssemblies.Prefix).ShouldContain("Platform");
}

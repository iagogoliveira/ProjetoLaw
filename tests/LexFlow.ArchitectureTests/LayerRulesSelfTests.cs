namespace LexFlow.ArchitectureTests;

/// <summary>
/// Prova que as regras de <see cref="LayerRules"/> realmente falham diante de violações
/// (fixture em <c>Fixture/ArchFixture.cs</c>), evitando testes de arquitetura que passam por vacuidade.
/// </summary>
public sealed class LayerRulesSelfTests
{
    private const string Prefix = "ArchFixture";

    private static IReadOnlyList<string> FixtureViolations => LayerRules.Check([typeof(LayerRulesSelfTests).Assembly], Prefix);

    [Fact]
    public void Modules_are_discovered_from_namespaces() =>
        LayerRules.DiscoverModules([typeof(LayerRulesSelfTests).Assembly], Prefix).ShouldBe(["Billing", "Crm"]);

    [Fact]
    public void Rules_detect_domain_depending_on_infrastructure() =>
        FixtureViolations.ShouldContain(v => v.Contains("ArchFixture.Billing.Domain.Invoice", StringComparison.Ordinal));

    [Fact]
    public void Rules_detect_cross_module_dependency_outside_contracts() =>
        FixtureViolations.ShouldContain(v => v.Contains("ArchFixture.Crm.Application.ChargeClientHandler", StringComparison.Ordinal));

    [Fact]
    public void Rules_allow_dependency_on_other_module_contracts() =>
        FixtureViolations.ShouldNotContain(v => v.Contains("InvoiceIssuedConsumer", StringComparison.Ordinal));
}

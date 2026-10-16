namespace LexFlow.ArchitectureTests;

/// <summary>Regras do CLAUDE.md: sem DateTime.Now (use IClock), sem SQL concatenado, IgnoreQueryFilters só em Platform.</summary>
public sealed class BannedApiTests
{
    [Fact]
    public void Production_code_does_not_call_banned_apis()
    {
        var violations = LexFlowAssemblies.All
            .SelectMany(a => BannedApiScanner.FindViolations(a, LexFlowAssemblies.Prefix))
            .ToList();

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Scanner_detects_planted_violations()
    {
        var violations = BannedApiScanner.FindViolations(typeof(BannedApiTests).Assembly, "ArchFixture.Banned");

        violations.ShouldContain(v => v.Contains("System.DateTime::get_Now", StringComparison.Ordinal));
        violations.ShouldContain(v => v.Contains("System.DateTimeOffset::get_UtcNow", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyzer_banned_symbol_files_exist_at_repository_root()
    {
        var root = FindRepositoryRoot();

        File.ReadAllText(Path.Combine(root, "BannedSymbols.txt")).ShouldContain("P:System.DateTime.Now");
        File.ReadAllText(Path.Combine(root, "BannedSymbols.TenantFilter.txt")).ShouldContain("IgnoreQueryFilters");
        File.ReadAllText(Path.Combine(root, "Directory.Build.props")).ShouldContain("BannedSymbols.txt");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LexFlow.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

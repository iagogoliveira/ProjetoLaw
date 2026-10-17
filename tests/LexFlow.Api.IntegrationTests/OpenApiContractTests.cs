using System.Text.Json;
using System.Text.Json.Nodes;

namespace LexFlow.Api.IntegrationTests;

/// <summary>
/// Snapshot do contrato (seção 13.1): o documento servido pela API deve ser igual ao <c>docs/openapi/v1.json</c>
/// versionado, de onde saem o cliente TypeScript e a checagem de breaking changes (oasdiff) no CI.
/// Para atualizar após mudar a API: <c>LEXFLOW_UPDATE_OPENAPI=1 dotnet test --project tests/LexFlow.Api.IntegrationTests</c>.
/// </summary>
public sealed class OpenApiContractTests(DependenciesFixture dependencies)
{
    public const string UpdateVariable = "LEXFLOW_UPDATE_OPENAPI";

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public async Task Served_document_matches_the_committed_contract()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();
        var served = Normalize(await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken));
        var path = Path.Combine(RepositoryRoot(), "docs", "openapi", "v1.json");

        if (string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, served, TestContext.Current.CancellationToken);
        }

        File.Exists(path).ShouldBeTrue($"Contrato ausente. Gere com {UpdateVariable}=1.");
        var committed = (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        if (!string.Equals(served, committed, StringComparison.Ordinal))
        {
            Assert.Fail($"A API mudou: revise e atualize docs/openapi/v1.json com {UpdateVariable}=1 (e regenere o cliente TypeScript).\n{FirstDifference(committed, served)}");
        }
    }

    /// <summary>A primeira linha diferente, com contexto (o documento inteiro é grande demais para a mensagem).</summary>
    private static string FirstDifference(string committed, string served)
    {
        var expected = committed.Split('\n');
        var actual = served.Split('\n');
        var line = 0;
        while (line < expected.Length && line < actual.Length && string.Equals(expected[line], actual[line], StringComparison.Ordinal))
        {
            line++;
        }

        string Context(string[] lines) => string.Join('\n', lines.Skip(Math.Max(0, line - 3)).Take(7));
        return $"Primeira diferença na linha {line + 1}.\n--- contrato versionado:\n{Context(expected)}\n--- servido:\n{Context(actual)}";
    }

    /// <summary>Remove o que varia por ambiente (servidores) e fixa a formatação.</summary>
    private static string Normalize(string json)
    {
        var document = JsonNode.Parse(json)!.AsObject();
        document.Remove("servers");

        // Comentários XML de várias linhas viram descrições com a quebra de linha do sistema que compilou
        // (\r\n no Windows, \n no Linux do CI): dentro das strings, vale sempre \n.
        return document.ToJsonString(Indented).Replace("\\r\\n", "\\n", StringComparison.Ordinal).ReplaceLineEndings("\n") + "\n";
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LexFlow.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}

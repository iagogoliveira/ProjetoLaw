using System.Net;

namespace LexFlow.Api.IntegrationTests;

public sealed class HealthEndpointsTests(DependenciesFixture dependencies)
{
    private const string UnreachableStorage = "http://127.0.0.1:1/";

    [Fact]
    public async Task Live_returns_200()
    {
        using var response = await GetAsync(dependencies.Settings, "/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_returns_200_when_all_dependencies_are_up()
    {
        using var response = await GetAsync(dependencies.Settings, "/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    /// <summary>Cada dependência é apontada para uma porta fechada; o readiness precisa falhar.</summary>
    [Theory]
    [InlineData("ConnectionStrings:lexflow", "Host=127.0.0.1;Port=1;Username=x;Password=x;Database=x;Timeout=2")]
    [InlineData("ConnectionStrings:redis", "127.0.0.1:1,abortConnect=false,connectTimeout=1000")]
    [InlineData("ConnectionStrings:rabbit", "amqp://guest:guest@127.0.0.1:1/")]
    [InlineData("Storage:ServiceUrl", UnreachableStorage)]
    public async Task Ready_returns_503_when_a_dependency_is_down(string key, string unreachableValue)
    {
        using var response = await GetAsync(With(key, unreachableValue), "/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Live_stays_200_when_a_dependency_is_down()
    {
        using var response = await GetAsync(With("Storage:ServiceUrl", UnreachableStorage), "/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private Dictionary<string, string> With(string key, string value) =>
        new(dependencies.Settings, StringComparer.Ordinal) { [key] = value };

    private static async Task<HttpResponseMessage> GetAsync(IReadOnlyDictionary<string, string> settings, string path)
    {
        await using var factory = new LexFlowApiFactory(settings);
        using var client = factory.CreateClient();
        return await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
    }
}

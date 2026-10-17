using System.Net;

namespace LexFlow.Api.IntegrationTests;

/// <summary>CORS só para a origem do SPA (seção 7.3), que chama o endpoint de token e a API de outra origem.</summary>
public sealed class CorsTests(DependenciesFixture dependencies)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static HttpRequestMessage Preflight(string path, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }

    [Theory]
    [InlineData("/connect/token")]
    [InlineData("/api/v1/me")]
    public async Task Spa_origin_is_allowed(string path)
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();
        using var request = Preflight(path, "http://localhost:4200");

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("http://localhost:4200");
        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    [Fact]
    public async Task Other_origins_are_not_allowed()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();
        using var request = Preflight("/connect/token", "https://evil.example");

        using var response = await client.SendAsync(request, Ct);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}

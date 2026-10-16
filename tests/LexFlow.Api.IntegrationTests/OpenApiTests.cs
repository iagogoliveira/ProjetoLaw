using System.Net;

namespace LexFlow.Api.IntegrationTests;

public sealed class OpenApiTests(DependenciesFixture dependencies)
{
    [Fact]
    public async Task OpenApi_document_is_served_in_development()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("\"openapi\"");
    }

    [Fact]
    public async Task Scalar_ui_is_served_in_development()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/scalar/v1", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

using System.Net;
using System.Text.Json;
using LexFlow.Application.Abstractions;
using LexFlow.Web.Common.Correlation;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.BuildingBlocks.UnitTests.Web;

public sealed class SecurityAndCorrelationTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private static void MapSample(WebApplication app)
    {
        app.MapGet("/protected", (ICurrentUser user, ITenantContext tenant) => new { user.UserId, tenant.OrganizationId })
            .RequirePermission("sample.things.read");
        app.MapGet("/public", () => "ok");
        app.MapGet("/signup", () => "ok").AllowPublicAccess("cadastro aberto");
    }

    [Fact]
    public async Task Without_authentication_configured_protected_endpoint_returns_401_problem()
    {
        await using var app = await TestWebHost.StartAsync(MapSample);
        using var client = app.GetTestClient();

        using var response = await client.GetAsync(new Uri("/protected", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        body.GetProperty("code").GetString().ShouldBe("auth.unauthenticated");
    }

    [Fact]
    public async Task Permission_is_recorded_in_endpoint_metadata()
    {
        await using var app = await TestWebHost.StartAsync(MapSample);

        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/protected");

        endpoint.Metadata.GetMetadata<RequiredPermissionAttribute>()!.Permission.ShouldBe("sample.things.read");
    }

    [Fact]
    public async Task Public_endpoint_records_its_reason_and_skips_authentication()
    {
        await using var app = await TestWebHost.StartAsync(MapSample);
        using var client = app.GetTestClient();

        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/signup");

        endpoint.Metadata.GetMetadata<PublicEndpointAttribute>()!.Reason.ShouldBe("cadastro aberto");
        endpoint.Metadata.GetMetadata<RequiredPermissionAttribute>().ShouldBeNull();
        (await client.GetStringAsync(new Uri("/signup", UriKind.Relative), Ct)).ShouldBe("ok");
    }

    [Fact]
    public async Task Valid_correlation_id_is_echoed()
    {
        await using var app = await TestWebHost.StartAsync(MapSample);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/public");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "req-123_abc.9");

        using var response = await client.SendAsync(request, Ct);

        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single().ShouldBe("req-123_abc.9");
    }

    [Theory]
    [InlineData("")]
    [InlineData("valor com espaço")]
    [InlineData("quebra\\nde-linha")]
    [InlineData("<script>")]
    public async Task Unsafe_or_missing_correlation_id_is_replaced(string incoming)
    {
        await using var app = await TestWebHost.StartAsync(MapSample);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/public");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, incoming);

        using var response = await client.SendAsync(request, Ct);

        var echoed = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
        echoed.ShouldNotBe(incoming);
        CorrelationIdMiddleware.IsAcceptable(echoed).ShouldBeTrue();
    }

    [Fact]
    public void Correlation_id_longer_than_64_chars_is_rejected() =>
        CorrelationIdMiddleware.IsAcceptable(new string('a', 65)).ShouldBeFalse();

    [Fact]
    public async Task Anonymous_endpoint_still_works()
    {
        await using var app = await TestWebHost.StartAsync(MapSample);
        using var client = app.GetTestClient();

        (await client.GetStringAsync(new Uri("/public", UriKind.Relative), Ct)).ShouldBe("ok");
    }

    [Fact]
    public async Task Tenant_context_without_claim_has_no_tenant()
    {
        await using var app = await TestWebHost.StartAsync(a => a.MapGet("/tenant", (ITenantContext t) => Results.Ok(t.HasTenant)), withTestAuthentication: true);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/tenant");
        request.Headers.Add(TestWebHost.UserHeader, Guid.CreateVersion7().ToString());

        (await (await client.SendAsync(request, Ct)).Content.ReadAsStringAsync(Ct)).ShouldBe("false");
    }
}

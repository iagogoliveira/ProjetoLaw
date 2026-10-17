using System.Net;
using System.Text.Json;
using LexFlow.Infrastructure.Common.Context;
using LexFlow.Platform.Domain.Jobs;
using LexFlow.Platform.Endpoints;
using LexFlow.Platform.Infrastructure.Persistence;
using LexFlow.Web.Common.Security;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LexFlow.Api.IntegrationTests;

/// <summary><c>GET /api/v1/jobs/{id}</c> pela API real: permissão, isolamento de tenant e contrato da resposta.</summary>
public sealed class JobsEndpointTests(DependenciesFixture dependencies)
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private async Task<Guid> CreateJobAsync(Guid organization, Action<Job>? mutate = null)
    {
        var id = Guid.CreateVersion7();
        using var ambient = AmbientContext.Begin(organization, null);
        await using var provider = dependencies.PlatformServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var job = Job.Enqueue(id, "documents.import");
        mutate?.Invoke(job);
        db.Jobs.Add(job);
        await db.SaveChangesAsync(Ct);
        return id;
    }

    /// <summary>Requisição de um membro (com <c>platform.jobs.read</c>) da organização dele.</summary>
    private static HttpRequestMessage Get(Guid id, SeededMember? member)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/jobs/{id}");
        if (member is not null)
        {
            request.Headers.Add(TestAuthenticationHandler.UserHeader, member.UserId.ToString());
            request.Headers.Add(TestAuthenticationHandler.OrganizationHeader, member.OrganizationId.ToString());
        }

        return request;
    }

    private Task<SeededMember> MemberAsync() => TestOrganizations.CreateWithMemberAsync(dependencies, PlatformPermissions.JobsRead);

    [Fact]
    public async Task Anonymous_request_returns_401()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();
        using var request = Get(Guid.CreateVersion7(), member: null);

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Own_job_is_returned_with_api_status()
    {
        var member = await MemberAsync();
        var id = await CreateJobAsync(member.OrganizationId, job => job.Start(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero)));
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();
        using var request = Get(id, member);

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
        body.GetProperty("id").GetGuid().ShouldBe(id);
        body.GetProperty("type").GetString().ShouldBe("documents.import");
        body.GetProperty("status").GetString().ShouldBe("running");
    }

    [Fact]
    public async Task Job_of_another_organization_returns_404()
    {
        var id = await CreateJobAsync((await MemberAsync()).OrganizationId);
        var outsider = await MemberAsync();
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();
        using var request = Get(id, outsider);

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("code").GetString().ShouldBe("platform.job_not_found");
    }

    [Fact]
    public async Task Unknown_job_returns_404()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);
        using var client = factory.CreateClient();
        using var request = Get(Guid.CreateVersion7(), await MemberAsync());

        (await client.SendAsync(request, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoint_requires_the_jobs_read_permission()
    {
        await using var factory = new LexFlowApiFactory(dependencies.Settings);

        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "/api/v1/jobs/{id:guid}");

        endpoint.Metadata.GetMetadata<RequiredPermissionAttribute>()!.Permission.ShouldBe(PlatformPermissions.JobsRead);
    }
}
